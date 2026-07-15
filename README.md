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

Die Lösung besteht aus zwei Projekten:

| Projekt          | Verantwortung                                                        |
| ---------------- | -------------------------------------------------------------------- |
| `VmPortal.Api`   | REST-API-Schicht (ASP.NET Core 8): Controller, Middleware, DI, Auth  |
| `VmPortal.Core`  | Fachlogik: Interfaces, Models, Services, Konfiguration               |

Kern des Entwurfs ist die Schnittstelle **`IVirtualizationProvider`** als Abstraktion über den
Hypervisor. `HyperVProvider` ist die konkrete Implementierung für Microsoft Hyper-V;
`DummyVirtualizationProvider` dient der lokalen Entwicklung. Ein alternativer `ProxmoxProvider`
wäre über dieselbe Schnittstelle möglich — diese Plattformunabhängigkeit ist der
wissenschaftliche Kern der Arbeit.

```
HTTP-Client → VmController → IVirtualizationProvider ─┬─ HyperVProvider  (WinRM/PowerShell)
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
`502` Hyper-V-Host nicht erreichbar.

## Voraussetzungen

- .NET SDK 8.0
- Für den Produktivbetrieb: Windows Server 2022 mit Hyper-V-Rolle und aktiviertem
  WinRM-HTTPS-Listener (Port 5986)

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

## Konfiguration

```jsonc
{
  "Virtualization": { "Provider": "HyperV" },   // oder "Dummy"
  "Ldap":  { "Host": "…", "Port": 389, "BaseDn": "DC=…,DC=…" },
  "HyperV": {
    "Host": "192.168.122.196",
    "Port": 5986,
    "Username": "testumgebung\\Administrator",
    "Password": "…",
    "CertificateThumbprint": "…",
    "UseSsl": true
  },
  "Jwt": { "Secret": "…", "Issuer": "VmPortal.Api", "Audience": "VmPortal.Client", "ExpiryHours": 8 }
}
```

> **Hinweis:** `appsettings.json` enthält in der Testumgebung Klartext-Secrets. In Produktion
> gehören diese in Umgebungsvariablen bzw. einen Secret-Store (siehe Phase 5).

## Deployment-Modell

Die Entwicklung erfolgt unter Ubuntu; die Anwendung wird auf einem Windows Server 2022
ausgeführt. Die Windows-spezifischen APIs (PowerShell-Remoting, WinRM, Hyper-V-Cmdlets)
funktionieren dort nativ. Der Build läuft plattformübergreifend; die WSMan-Clientbibliothek
ist ausschließlich unter Windows zur Laufzeit verfügbar.

## Projektstand

| Meilenstein | Inhalt                                              | Status |
| ----------- | --------------------------------------------------- | ------ |
| M1          | Projektstruktur, Interfaces, Models                 | ✅     |
| M2          | Dummy-Services, DI, testbare API                    | ✅     |
| M3          | LDAP-Auth, JWT, geschützte Endpunkte, RBAC          | ✅     |
| M4          | Hyper-V-Anbindung über WinRM/PowerShell             | ✅     |
| M5–M7       | Persistenz, Frontend, Evaluation (siehe CLAUDE.md)  | ⏳     |
