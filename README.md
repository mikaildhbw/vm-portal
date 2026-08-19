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
Hypervisor. `HyperVProvider` ist die konkrete Implementierung für Microsoft Hyper-V — mit zwei
Modi: `Local` (App läuft direkt auf dem Hyper-V-Host, lokale PowerShell-Ausführung) und
`Remote` (App läuft auf einem separaten Server, steuert mehrere Hyper-V-Hosts über
WinRM/Kerberos an — der produktiv genutzte Modus, siehe [Voraussetzungen](#voraussetzungen)).
`DummyVirtualizationProvider` dient der lokalen Entwicklung. Ein alternativer `ProxmoxProvider`
wäre über dieselbe Schnittstelle möglich — diese Plattformunabhängigkeit ist der
wissenschaftliche Kern der Arbeit.

```
HTTP-Client → VmController → IDbAuthorizationService (DB-first: welche VMs darf der Nutzer sehen?)
                            → IVirtualizationProvider ─┬─ HyperVProvider  (Local: lokale PowerShell)
                                                        │                 (Remote: WinRM/Kerberos, Multi-Host)
                                                        └─ DummyProvider   (In-Memory)
```

Die VM-Liste wird DB-first ermittelt: `VmController.GetVms()` fragt zuerst per einziger
DB-Abfrage, welche (Host, VM)-Paare der angemeldete Nutzer sehen darf, und ruft den Hypervisor
erst danach gezielt nur dafür ab — statt das komplette Inventar aller Hosts zu holen und
anschließend jede VM einzeln zu autorisieren. Bootstrap-FullAdmin (z. B. `ESX Admins`) sieht
weiterhin das volle, ungefilterte Inventar.

## Sicherheit

- **Authentifizierung:** LDAP-Bind gegen Active Directory — `LdapAuthService`. Das Portal
  prüft nie selbst ein Passwort, sondern bindet sich als der jeweilige Benutzer ans AD.
  Domäne ist umgebungsabhängig konfiguriert (`Ldap:Host`/`BaseDn`): lokale Testumgebung
  `testumgebung.local`, Produktion die Siemens-Domäne `archiv.mhm.siemens.com` — dort seit
  dem 2026-08-19-Deployment erfolgreich verifiziert.
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
| GET     | `/api/admin/vm-groups/{id}/members`        | Mitglieder der VM-Gruppe (Host + VM-Name/GUID) |
| POST    | `/api/admin/vm-groups/{id}/members`        | Eine oder mehrere VMs hinzufügen (legt sie bei Bedarf neu an) |
| DELETE  | `/api/admin/vm-groups/{id}/members/{memberId}` | VM aus der Gruppe entfernen (Gruppe wird `null`, Eintrag bleibt) |
| GET     | `/api/admin/ad-groups?search=`             | AD-Gruppen durchsuchen (Autocomplete-Vorstufe) |
| GET     | `/api/admin/discover-vms`                  | Voller Hypervisor-Bestand vs. DB-Stand (read-only Vorschau) |

Antwortcodes: `401` nicht angemeldet, `403` keine Bootstrap-FullAdmin-Mitgliedschaft
(AD-Gruppe aus `Authorization:BootstrapFullAdminGroup`), sonst wie bei einer typischen
REST-API (`200`/`201`/`204`/`400`/`404`); `/api/admin/ad-groups` zusätzlich `502` bei
LDAP-Fehlern (z. B. fehlender Service-Account, siehe [Konfiguration](#konfiguration)).

## Voraussetzungen

- .NET SDK 8.0
- Für den Produktivbetrieb: `HyperVProvider` im `Remote`-Modus (`Virtualization:HyperV:Mode`).
  Die App läuft auf einem separaten Windows-Server und steuert die drei produktiven
  Hyper-V-Hosts (`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`, FQDN-Muster
  `<hostname>.archiv.mhm.siemens.com`) über WinRM/Kerberos (Port 5985) an. **Implementiert
  und am 2026-08-19 auf der Produktions-VM erfolgreich gegen alle drei Hosts getestet**,
  inklusive Login gegen die Produktionsdomäne (`archiv.mhm.siemens.com`). Da VM-Namen nicht
  host-eindeutig sind (bestätigte Kollisionen zwischen Hosts), identifiziert der Remote-Modus
  VMs über Server + Hyper-V-VM-GUID statt über den Namen allein.
  Alternativ existiert weiterhin der ursprüngliche `Local`-Modus (Windows Server 2022 mit
  Hyper-V-Rolle, App läuft direkt auf dem Hyper-V-Host, Cmdlets über eine lokale
  PowerShell-Instanz) — für Deployments, bei denen App und Hyper-V-Host derselbe Rechner sind.

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

**Admin-Panel** (`/admin/*`, nur für FullAdmin sichtbar/erreichbar, baut auf den
Admin-Endpunkten aus dem Abschnitt „Administration“ oben auf): Rollen-Matrix
(`/admin/roles`), VM-Gruppen inkl. Mitgliederverwaltung (`/admin/vm-groups`,
`/admin/vm-groups/:groupId`) und Zuordnungen AD-Gruppe × VM-Gruppe × Rolle
(`/admin/permissions`). Da das JWT clientseitig nicht lesbar ist, gibt es keinen Claim für
den Zugriffscheck — das Frontend probt stattdessen `GET /api/admin/servers` (200 = Admin).
Die AD-Gruppen-Auswahl im Zuordnungen-Formular kann nur AD-Gruppen zuordnen, die bereits als
`UserGroup` in der DB bekannt sind (kein Endpunkt zum Anlegen neuer `UserGroups`, siehe
Projektstand-Tabelle unten) — die Live-AD-Suche dient dort nur als Autocomplete/Fallback-
Anzeige.

### Entwicklung

```bash
cd VmPortal.Frontend
npm install
npm run dev            # Dev-Server auf http://localhost:5173
```

Der Vite-Dev-Server proxyt `/api` an die echte API (`http://localhost:5000`, siehe
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
  "Virtualization": {
    "Provider": "HyperV",   // oder "Dummy"
    "HyperV": {              // optional - fehlt der Abschnitt, ist Mode implizit "Local"
      "Mode": "Remote",       // "Local" oder "Remote"
      "Hosts": [
        { "Name": "MHM-HYPERV1", "FQDN": "mhm-hyperv1.archiv.mhm.siemens.com" },
        { "Name": "MHM-HYPERV3", "FQDN": "mhm-hyperv3.archiv.mhm.siemens.com" },
        { "Name": "MHM-HYPERV4", "FQDN": "mhm-hyperv4.archiv.mhm.siemens.com" }
      ],
      "Remote": { "Port": 5985, "UseSsl": false, "Authentication": "Kerberos" }
    }
  },
  "Ldap":  {
    "Host": "…", "Port": 389, "BaseDn": "DC=…,DC=…",
    "ServiceAccountUsername": "…",   // optional, für GET /api/admin/ad-groups
    "ServiceAccountPassword": "…"    // ohne diese Felder: anonymer LDAP-Bind-Versuch
  },
  "Jwt": { "Secret": "…", "Issuer": "VmPortal.Api", "Audience": "VmPortal.Client", "ExpiryHours": 8 },
  "ConnectionStrings": { "VmPortalDb": "Data Source=vmportal.db" },
  "Authorization": { "BootstrapFullAdminGroup": "VM-Portal-Benutzer" }  // "ESX Admins" in Produktion
}
```

Ohne `Virtualization:HyperV`-Abschnitt (bzw. `Mode: "Local"`) führt der `HyperV`-Provider
PowerShell lokal aus und benötigt keine Verbindungskonfiguration — der bisherige,
weiterhin unterstützte Modus. Mit `Mode: "Remote"` (Produktivbetrieb,
`appsettings.Production.json`) verbindet er sich per WinRM/Kerberos zu jedem in `Hosts`
gelisteten Host; die Authentifizierung läuft über die Prozessidentität des ausführenden
Kontos, kein Credential in der Konfiguration.

`ConnectionStrings:VmPortalDb`, `Authorization:BootstrapFullAdminGroup`, `Ldap` und
`Virtualization:HyperV` sind umgebungsabhängig (`appsettings.json` = Testumgebung,
`appsettings.Production.json` überschreibt mit den Siemens-AD-/Hyper-V-Werten und dem
Produktionspfad `C:\VmPortal\data\vmportal.db`) — siehe
[`docs/authorization.md`](docs/authorization.md).

`Ldap:ServiceAccountUsername`/`ServiceAccountPassword` sind optional und werden nur von
`GET /api/admin/ad-groups` gebraucht (AD-Gruppensuche fürs Admin-Panel, läuft nicht im
Kontext eines eingeloggten Nutzers wie der Login selbst). Fehlen sie, wird ein anonymer
LDAP-Bind versucht — auf restriktiv konfigurierten ADs (z. B. der Siemens-Produktions-AD)
schlägt das vermutlich fehl, dann müssen hier echte Service-Account-Zugangsdaten hinterlegt
werden.

> **Hinweis:** `appsettings.json` enthält in der Testumgebung Klartext-Secrets. In Produktion
> gehören diese in Umgebungsvariablen bzw. einen Secret-Store (siehe Phase 5).

## Datenbank / Migrationen

Die SQLite-Autorisierungsschicht wird nicht automatisch beim App-Start migriert (bewusst,
siehe `docs/authorization.md`). Vor dem ersten Start bzw. nach jeder neuen Migration:

```bash
dotnet ef database update --project VmPortal.Core --startup-project VmPortal.Api
```

Voraussetzung: `dotnet tool install --global dotnet-ef`. Die Migration
`InitialAuthorizationSchema` legt Schema **und** Grund-Seed-Daten an (fünf System-Rollen,
alle 22 VM-Aktionen, die beiden Bootstrap-`UserGroups`, ursprünglich vier Hyper-V-Hosts).
Drei Folgemigrationen korrigieren/ergänzen das: `FixVirtualServersHostCount` entfernt den
vierten, nicht real existierenden Host-Eintrag (übrig: die drei echten Hosts
`MHM-HYPERV1`/`3`/`4`), `SeedTestUserPermissions` seedet eine Testberechtigung für die
neun Hyper-V-Test-VMs `HVP_1`–`HVP_9`, `AddVmGuidToVirtualMachines` ergänzt die optionale
Spalte `VmGuid` (befüllt über die Admin-API bei VM-Gruppen-Mitgliedschaft, siehe oben).

## Deployment-Modell

Produktivbetrieb läuft im `Remote`-Modus: Die Anwendung wird auf einem **separaten**
Windows-Server ausgeführt und steuert die drei Hyper-V-Hosts über WinRM/Kerberos an (kein
gemeinsamer Rechner mehr nötig). Alternativ unterstützt `HyperVProvider` weiterhin den
ursprünglichen `Local`-Modus, bei dem die Anwendung direkt auf dem Hyper-V-Host läuft und die
Cmdlets über eine lokale PowerShell-Instanz aufruft. Der Build läuft plattformübergreifend;
die Hyper-V-Cmdlets sind ausschließlich unter Windows zur Laufzeit verfügbar (lokal wie
remote — WinRM-Ziel ist immer ein Windows-Host mit Hyper-V-Rolle).

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
| M7          | WinRM-Multi-Host-Remote-Modus (Produktivbetrieb, gegen alle 3 Hosts getestet); Login gegen Produktionsdomäne; DB-first-Autorisierung statt Full-Inventory-Scan | ✅     |
| M8          | Admin-Backend für Rechtevergabe-Matrix (AD-Gruppensuche, VM-Gruppen-Mitgliederverwaltung, VM-Discovery) | ✅     |
| M9          | Admin-Panel-Frontend (Rollen-Matrix, VM-Gruppen, Zuordnungen unter `/admin/*`, FullAdmin-Gate) | ✅     |
| M9 (Rest)   | LDAP-Service-Account für AD-Gruppensuche in Produktion hinterlegen; fehlender Backend-Endpunkt für neue `UserGroups` (siehe Konfiguration/Frontend oben) | ⏳     |
| M10         | Evaluation (siehe CLAUDE.md)                        | ⏳     |
