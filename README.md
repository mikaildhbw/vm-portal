# VmPortal

Self-Service-Portal für virtuelle Maschinen (Hyper-V), entwickelt im Rahmen einer
Bachelorarbeit bei der Siemens AG.

> **Forschungsfrage:** Wie wird ein sicheres Self-Service-Portal für virtuelle Maschinen
> konzipiert, implementiert und hinsichtlich Sicherheit, Benutzerfreundlichkeit und
> Plattformunabhängigkeit bewertet?

Angemeldete Benutzer verwalten über eine REST-API ausschließlich die ihnen zugewiesenen
virtuellen Maschinen: starten, stoppen, Snapshots erstellen, Ressourcen anpassen u. v. m.
Authentifizierung erfolgt gegen Active Directory, die Autorisierung rollenbasiert (RBAC).

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

- **Authentifizierung:** LDAP-Bind gegen Active Directory (`testumgebung.local`) —
  `LdapAuthService`. Das Portal prüft nie selbst ein Passwort, sondern bindet sich als der
  jeweilige Benutzer ans AD.
- **Token:** JWT (HMAC-SHA256), Claims `username`, `role`, `vmroles` (VM-Name → Rolle, aus
  AD-Gruppen nach dem Schema `VM-{VmName}-{Rolle}`) und `adgroups` (rohe
  AD-Gruppennamen), Gültigkeit 8 Stunden.
- **Transport:** JWT in einem `httpOnly`-Cookie (`SameSite=Strict`, `Secure`) — kein
  `localStorage`, dadurch kein Zugriff durch JavaScript (XSS-Schutz).
- **Middleware:** Alle Endpunkte außer `/api/auth/login` sind mit `[Authorize]` geschützt.
- **RBAC (`VmController`):** VM-Autorisierung läuft ausschließlich über die persistente
  SQLite-Autorisierungsschicht (`DbAuthorizationService`, EF Core): AD-Gruppen aus dem
  `adgroups`-Claim werden Rollen auf VM-Gruppen zugeordnet, Rollen definieren sich über eine
  Rolle×Aktion-Matrix mit System- und frei erstellbaren Custom-Rollen, verwaltet über die
  `/api/admin/*`-Endpunkte. Ohne passende Zuordnung: implizite Nicht-Berechtigung, `403
  Forbidden`. Details: [`docs/authorization.md`](docs/authorization.md).
- **`vmroles`-Claim:** Wird weiterhin erzeugt (VM-Name → Rolle aus AD-Gruppen nach dem Schema
  `VM-{VmName}-{Rolle}`), fließt aber seit der Umstellung auf `DbAuthorizationService` in
  keine Autorisierungsentscheidung mehr ein — aktuell ohne Konsumenten, reserviert für eine
  mögliche Frontend-Anzeige.
- **Konfiguration:** Sämtliche Secrets und Verbindungsdaten liegen in `appsettings.json`,
  nichts ist im Code hartcodiert.

## API-Endpunkte

### Auth (`AuthController`)

| Methode | Pfad                | Beschreibung                     | Auth |
| ------- | -------------------- | --------------------------------- | ---- |
| POST    | `/api/auth/login`    | Login gegen AD, setzt JWT-Cookie  | –    |
| POST    | `/api/auth/logout`   | Löscht das JWT-Cookie             | ✓    |

### VMs (`VmController`, RBAC über `DbAuthorizationService`/`adgroups`-Claim)

| Methode | Pfad                              | Beschreibung                    |
| ------- | ---------------------------------- | -------------------------------- |
| GET     | `/api/vm`                          | VMs, für die eine Rolle vorliegt (mind. Viewer) |
| GET     | `/api/vm/{id}`                     | VM-Details                       |
| GET     | `/api/vm/{id}/metering`            | Ressourcenverbrauch (`Measure-VM`) |
| POST    | `/api/vm/{id}/start`               | VM starten                       |
| POST    | `/api/vm/{id}/stop`                | VM stoppen (`-Force`)            |
| POST    | `/api/vm/{id}/pause`               | VM pausieren                     |
| POST    | `/api/vm/{id}/resume`              | VM fortsetzen                    |
| POST    | `/api/vm/{id}/save-state`          | VM-Zustand speichern             |
| POST    | `/api/vm/{id}/reset`               | VM neu starten (`-Force`)        |
| POST    | `/api/vm/{id}/snapshot`            | Snapshot/Checkpoint erstellen    |
| POST    | `/api/vm/{id}/snapshot/apply`      | Snapshot anwenden                |
| DELETE  | `/api/vm/{id}/snapshot/{name}`     | Snapshot löschen                 |
| POST    | `/api/vm/{id}/console`             | Konsolenverbindung (`501`, nicht implementiert) |
| POST    | `/api/vm/{id}/resize-ram`          | Arbeitsspeicher anpassen         |
| POST    | `/api/vm/{id}/resize-cpu`          | CPU-Anzahl anpassen              |
| POST    | `/api/vm/{id}/network-adapter`     | Netzwerkadapter anhängen         |
| POST    | `/api/vm/{id}/vhd/resize`          | Virtuelle Festplatte vergrößern  |
| POST    | `/api/vm/{id}/vhd/compact`         | Virtuelle Festplatte komprimieren |
| POST    | `/api/vm/{id}/export`              | VM exportieren                   |
| POST    | `/api/vm/{id}/import`              | VM importieren                   |
| POST    | `/api/vm/{id}/clone`               | VM klonen (`501`, nicht implementiert) |
| POST    | `/api/vm/{id}/migrate`             | Live-Migration (`501`, nicht implementiert) |

Alle VM-Endpunkte erfordern `[Authorize]`. Antwortcodes: `200`/`204` Erfolg, `401` nicht
angemeldet, `403` keine ausreichende Rolle auf der VM, `404` VM unbekannt, `501` bewusst
nicht implementierte Aktion, `502` Hyper-V-Ausführung fehlgeschlagen.

### Administration (`Controllers/Admin/*`, nur Bootstrap-FullAdmin)

Verwalten die SQLite-Autorisierungsschicht (Rollen, Zuordnungen, VM-Gruppen, Server) —
siehe [`docs/authorization.md`](docs/authorization.md) für das Datenmodell.

| Methode | Pfad                          | Beschreibung                                 |
| ------- | ------------------------------ | --------------------------------------------- |
| GET     | `/api/admin/roles`             | Alle Rollen inkl. `IsSystemRole` und Aktionen |
| POST    | `/api/admin/roles`             | Custom-Rolle anlegen (optional `cloneFromRoleId`) |
| PUT     | `/api/admin/roles/{id}`        | Aktionsliste einer Custom-Rolle ersetzen (`400` bei System-Rolle) |
| DELETE  | `/api/admin/roles/{id}`        | Custom-Rolle löschen (`400` bei System-Rolle oder aktiver Nutzung) |
| GET     | `/api/admin/permissions`       | Alle Zuordnungen (UserGroup × VmGroup × Role) |
| POST    | `/api/admin/permissions`       | Zuordnung anlegen                             |
| DELETE  | `/api/admin/permissions/{id}`  | Zuordnung löschen                             |
| GET     | `/api/admin/vm-groups`         | Alle VM-Gruppen                               |
| GET     | `/api/admin/vm-groups/{id}`    | VM-Gruppe inkl. Mitglieder                    |
| POST    | `/api/admin/vm-groups`         | VM-Gruppe anlegen                             |
| PUT     | `/api/admin/vm-groups/{id}`    | VM-Gruppe umbenennen                          |
| DELETE  | `/api/admin/vm-groups/{id}`    | VM-Gruppe löschen                             |
| GET     | `/api/admin/servers`           | Alle Hyper-V-Hosts                            |
| POST    | `/api/admin/servers`           | Hyper-V-Host anlegen                          |

Antwortcodes: `401` nicht angemeldet, `403` keine Bootstrap-FullAdmin-Mitgliedschaft
(AD-Gruppe aus `Authorization:BootstrapFullAdminGroup`), sonst wie bei einer typischen
REST-API (`200`/`201`/`204`/`400`/`404`).

## Voraussetzungen

- .NET SDK 8.0
- Für den Produktivbetrieb: Windows Server 2022 mit Hyper-V-Rolle. Die App läuft direkt auf
  dem Hyper-V-Host und ruft die Hyper-V-Cmdlets über eine lokale PowerShell-Instanz auf —
  ein Remote-Modus über WinRM ist im aktuellen Code nicht implementiert. Die
  WinRM/Kerberos-Konnektivität (Port 5985) zu den drei produktiven Hyper-V-Hosts
  (`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`) wurde am 2026-08-19 verifiziert, ist aber
  (noch) nicht angebunden. Da VM-Namen nicht host-eindeutig sind, muss eine künftige
  VM-Identifikation über Server + Hyper-V-VM-GUID statt über den Namen allein erfolgen.

## Bauen und Starten

```bash
# Bauen (funktioniert auf Linux und Windows)
dotnet build VmPortal.sln

# Lokal starten mit Dummy-Provider (automatisierte Tests, infrastrukturunabhängige Entwicklung)
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
  "Jwt": { "Secret": "…", "Issuer": "VmPortal.Api", "Audience": "VmPortal.Client", "ExpiryHours": 8 },
  "ConnectionStrings": { "VmPortalDb": "Data Source=vmportal.db" },
  "Authorization": { "BootstrapFullAdminGroup": "VM-Portal-Benutzer" }  // "ESX Admins" in Produktion
}
```

Der `HyperV`-Provider führt PowerShell lokal aus und benötigt daher keine
Verbindungskonfiguration (kein Host/Port/Zugangsdaten).

`ConnectionStrings:VmPortalDb` und `Authorization:BootstrapFullAdminGroup` sind
umgebungsabhängig (`appsettings.json` = Testumgebung, `appsettings.Production.json`
überschreibt mit dem Siemens-AD-Wert und dem Produktionspfad
`C:\VmPortal\data\vmportal.db`) — siehe [`docs/authorization.md`](docs/authorization.md).

> **Hinweis:** `appsettings.json` enthält in der Testumgebung Klartext-Secrets. In Produktion
> gehören diese in Umgebungsvariablen bzw. einen Secret-Store (siehe Phase 5).

## Datenbank / Migrationen

Die SQLite-Autorisierungsschicht wird nicht automatisch beim App-Start migriert (bewusst,
siehe `docs/authorization.md`). Vor dem ersten Start bzw. nach jeder neuen Migration:

```bash
dotnet ef database update --project VmPortal.Core --startup-project VmPortal.Api
```

Voraussetzung: `dotnet tool install --global dotnet-ef`. Die Migration
`InitialAuthorizationSchema` legt Schema **und** Seed-Daten an (fünf System-Rollen, alle
22 VM-Aktionen, die vier Hyper-V-Hosts, die beiden Bootstrap-`UserGroups`).

## Deployment-Modell

Die Anwendung wird auf einem Windows Server 2022 ausgeführt, der zugleich der Hyper-V-Host
ist. Die Hyper-V-Cmdlets werden über eine lokale PowerShell-Instanz aufgerufen und
funktionieren dort nativ. Der Build läuft plattformübergreifend; die Hyper-V-Cmdlets sind
ausschließlich unter Windows zur Laufzeit verfügbar.

## Projektstand

| Meilenstein | Inhalt                                              | Status |
| ----------- | --------------------------------------------------- | ------ |
| M1          | Projektstruktur, Interfaces, Models                 | ✅     |
| M2          | Dummy-Services, DI, testbare API                    | ✅     |
| M3          | LDAP-Auth, JWT, geschützte Endpunkte, RBAC          | ✅     |
| M4          | Hyper-V-Anbindung über lokale PowerShell            | ✅     |
| M5          | React-Frontend (Login, Übersicht, Detail)           | ✅     |
| M6          | SQLite/EF-Core-Autorisierungsschicht (RBAC, Admin-API); `VmController` auf DB-Autorisierung umgestellt | ✅     |
| M6 (Rest)   | Audit-Log, Secret-Store, vollständige `GroupPermissions` in der Testumgebung | ⏳     |
| M7          | Evaluation (siehe CLAUDE.md)                        | ⏳     |
