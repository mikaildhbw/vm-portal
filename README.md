# VmPortal

Self-Service-Portal für virtuelle Maschinen (Hyper-V), entwickelt im Rahmen einer
Bachelorarbeit bei der Siemens AG.

> **Forschungsfrage:** Wie wird ein sicheres Self-Service-Portal für virtuelle Maschinen
> konzipiert, implementiert und hinsichtlich Sicherheit, Benutzerfreundlichkeit und
> Plattformunabhängigkeit bewertet?

Angemeldete Benutzer verwalten über eine REST-API ausschließlich die ihnen zugewiesenen
virtuellen Maschinen: starten, stoppen, neu starten und Snapshots erstellen. Authentifizierung
erfolgt gegen Active Directory, die Autorisierung rollenbasiert.

## Architektur

Die Lösung besteht aus drei Projekten:

| Projekt             | Verantwortung                                                        |
| ------------------- | -------------------------------------------------------------------- |
| `VmPortal.Api`      | REST-API-Schicht (ASP.NET Core 8): Controller, Middleware, DI, Auth  |
| `VmPortal.Core`     | Fachlogik: Interfaces, Models, Services, Konfiguration               |
| `VmPortal.Frontend` | React-SPA (Vite, plain JavaScript): Login, VM-Übersicht, VM-Detail   |

Kern des Entwurfs ist die Schnittstelle **`IVirtualizationProvider`** als Abstraktion über den
Hypervisor. `HyperVProvider` ist die konkrete Implementierung für Microsoft Hyper-V;
`DummyVirtualizationProvider` dient der lokalen Entwicklung. Ein alternativer `ProxmoxProvider`
wäre über dieselbe Schnittstelle möglich — diese Plattformunabhängigkeit ist der
wissenschaftliche Kern der Arbeit.

```
HTTP-Client → VmController → IVirtualizationProvider ─┬─ HyperVProvider  (lokale PowerShell)
                                                      └─ DummyProvider   (In-Memory)
```

## Sicherheit

- **Authentifizierung:** LDAP-Bind gegen Active Directory (`testumgebung.local`).
- **Token:** JWT (HMAC-SHA256), Claims `username` und `role`, Gültigkeit 8 Stunden.
- **Transport:** JWT in einem `httpOnly`-Cookie (`SameSite=Strict`, `Secure`) — kein
  `localStorage`, dadurch kein Zugriff durch JavaScript (XSS-Schutz).
- **Middleware:** Alle Endpunkte außer `/api/auth/login` sind mit `[Authorize]` geschützt.
- **RBAC:** Ein Benutzer sieht und steuert ausschließlich VMs, die ihm zugewiesen sind;
  Zugriff auf fremde VMs wird mit `403 Forbidden` beantwortet.
- **Konfiguration:** Sämtliche Secrets und Verbindungsdaten liegen in `appsettings.json`,
  nichts ist im Code hartcodiert.

## API-Endpunkte

| Methode | Pfad                       | Beschreibung                          | Auth |
| ------- | -------------------------- | ------------------------------------- | ---- |
| POST    | `/api/auth/login`          | Login gegen AD, setzt JWT-Cookie      | –    |
| POST    | `/api/auth/logout`         | Löscht das JWT-Cookie                 | ✓    |
| GET     | `/api/vm`                  | VMs des angemeldeten Benutzers        | ✓    |
| POST    | `/api/vm/{id}/start`       | VM starten                            | ✓    |
| POST    | `/api/vm/{id}/stop`        | VM stoppen (`-Force`)                 | ✓    |
| POST    | `/api/vm/{id}/reset`       | VM neu starten (`-Force`)             | ✓    |
| POST    | `/api/vm/{id}/snapshot`    | Snapshot/Checkpoint erstellen         | ✓    |

Antwortcodes bei VM-Operationen: `200` Erfolg, `403` fremde VM, `404` VM unbekannt,
`502` Hyper-V-Ausführung fehlgeschlagen.

## Voraussetzungen

- .NET SDK 8.0
- Für den Produktivbetrieb: Windows Server 2022 mit Hyper-V-Rolle. Die App läuft direkt auf
  dem Hyper-V-Host und ruft die Hyper-V-Cmdlets über eine lokale PowerShell-Instanz auf —
  kein WinRM/Remoting nötig.

## Bauen und Starten

```bash
# Bauen (funktioniert auf Linux und Windows)
dotnet build VmPortal.sln

# Lokal starten mit Dummy-Provider (Entwicklung, z. B. auf Ubuntu)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project VmPortal.Api

# Produktiv starten mit Hyper-V-Provider (auf Windows Server)
ASPNETCORE_ENVIRONMENT=Production dotnet run --project VmPortal.Api
```

Der aktive Hypervisor wird über `Virtualization:Provider` in der jeweiligen
`appsettings.*.json` gewählt (`HyperV` oder `Dummy`).

### Beispiel-Login

```bash
curl -c cookies.txt -X POST http://localhost:5165/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"mugur","password":"Test1234!"}'

curl -b cookies.txt http://localhost:5165/api/vm
```

## Frontend (VmPortal.Frontend)

React-SPA mit Vite (plain JavaScript). Das JWT liegt ausschließlich im httpOnly-Cookie;
Axios sendet es über `withCredentials: true` automatisch mit. Ein zentraler Interceptor leitet
bei `401` zur Login-Seite. Views: Login (`/`), VM-Übersicht (`/vms`), VM-Detail (`/vms/:id`
mit Status-Polling alle 5 s und Snapshot-Funktion).

### Entwicklung

```bash
cd VmPortal.Frontend
npm install
npm run dev            # Dev-Server auf http://localhost:5173
```

Der Vite-Dev-Server proxyt `/api` an die echte API (`http://192.168.122.196:5000`, siehe
`vite.config.js`), sodass Frontend und API unter derselben Origin laufen. Alternativ erlaubt
das Backend CORS für `http://localhost:5173` (konfigurierbar über `Cors:AllowedOrigins`).

### Produktions-Build und Auslieferung

```bash
cd VmPortal.Frontend
npm run build                       # erzeugt dist/
cp -r dist/* ../VmPortal.Api/wwwroot/   # in wwwroot der API kopieren
```

Die API liefert das Frontend über `UseStaticFiles()` aus; ein SPA-Fallback
(`MapFallbackToFile("index.html")`) beantwortet alle Nicht-API-Routen mit `index.html`, damit
das clientseitige Routing funktioniert. `VmPortal.Api/wwwroot/` ist generiertes Build-Output
und wird nicht versioniert.

## Konfiguration

```jsonc
{
  "Virtualization": { "Provider": "HyperV" },   // oder "Dummy"
  "Ldap":  { "Host": "…", "Port": 389, "BaseDn": "DC=…,DC=…" },
  "Jwt": { "Secret": "…", "Issuer": "VmPortal.Api", "Audience": "VmPortal.Client", "ExpiryHours": 8 }
}
```

Der `HyperV`-Provider führt PowerShell lokal aus und benötigt daher keine
Verbindungskonfiguration (kein Host/Port/Zugangsdaten).

> **Hinweis:** `appsettings.json` enthält in der Testumgebung Klartext-Secrets. In Produktion
> gehören diese in Umgebungsvariablen bzw. einen Secret-Store (siehe Phase 5).

## Deployment-Modell

Die Entwicklung erfolgt unter Ubuntu; die Anwendung wird auf dem Windows Server 2022
ausgeführt, der zugleich der Hyper-V-Host ist. Die Hyper-V-Cmdlets werden über eine lokale
PowerShell-Instanz aufgerufen und funktionieren dort nativ. Der Build läuft
plattformübergreifend; die Hyper-V-Cmdlets sind ausschließlich unter Windows zur Laufzeit
verfügbar.

## Projektstand

| Meilenstein | Inhalt                                              | Status |
| ----------- | --------------------------------------------------- | ------ |
| M1          | Projektstruktur, Interfaces, Models                 | ✅     |
| M2          | Dummy-Services, DI, testbare API                    | ✅     |
| M3          | LDAP-Auth, JWT, geschützte Endpunkte, RBAC          | ✅     |
| M4          | Hyper-V-Anbindung über lokale PowerShell            | ✅     |
| M5          | React-Frontend (Login, Übersicht, Detail)           | ✅     |
| M6–M7       | Persistenz, Evaluation (siehe CLAUDE.md)            | ⏳     |
