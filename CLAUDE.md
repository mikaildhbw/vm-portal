# VmPortal — Projektkontext für Claude Code

## Über das Projekt
Self-Service-Portal für virtuelle Maschinen (Hyper-V), Bachelorarbeit bei der Siemens AG.

**Forschungsfrage:** Wie wird ein sicheres Self-Service-Portal für virtuelle Maschinen
konzipiert, implementiert und hinsichtlich Sicherheit, Benutzerfreundlichkeit und
Plattformunabhängigkeit bewertet?

## Architektur
Drei Projekte, klare Schichtentrennung:

- **VmPortal.Api** — REST-API (ASP.NET Core 8): Controller, Middleware, DI, Authentifizierung;
  liefert im Produktivbetrieb zusätzlich das gebaute Frontend aus `wwwroot` aus.
- **VmPortal.Core** — Fachlogik: Interfaces, Models, Services, Konfigurationsklassen.
- **VmPortal.Frontend** — React-SPA (Vite, plain JavaScript): Login, VM-Übersicht, VM-Detail.

```
VmPortal.Core/
  Interfaces/     IVirtualizationProvider, IAuthService, ITokenService,
                  IDbAuthorizationService
  Models/         VirtualMachine, VmStatus, VmRole, VmAction
  Services/       HyperVProvider, DummyVirtualizationProvider,
                  LdapAuthService, DummyAuthService, JwtTokenService,
                  RolePermissions, VmRoleClaims, AdGroupClaims,
                  DbAuthorizationService, VirtualizationException
  Configuration/  LdapSettings, JwtSettings, AuthorizationSettings,
                  TestVmRolesSettings, TestAdGroupsSettings
  Data/           VmPortalDbContext, AuthorizationSeedData, Entities/,
                  Migrations/ (SQLite-Autorisierungsschicht, siehe
                  docs/authorization.md)
VmPortal.Api/
  Controllers/    AuthController, VmController
  Controllers/Admin/ RolesController, PermissionsController,
                  VmGroupsController, ServersController (FullAdmin-only)
  Middleware/     VirtualizationExceptionMiddleware
  Constants/      AuthConstants
  wwwroot/        gebautes React-Frontend (generiert, nicht versioniert)
  Program.cs      Composition Root (DI, Auth, CORS, Static/SPA, Provider-Auswahl,
                  DbContext-Registrierung)
VmPortal.Frontend/
  src/api/        client (Axios, withCredentials, 401-Interceptor), vmApi
  src/pages/      Login, VmList, VmDetail
  src/components/ Header
  vite.config.js  Dev-Proxy /api -> Windows-Server-API
```

### Zentrale Abstraktion
`IVirtualizationProvider` kapselt den Hypervisor. Konkrete Implementierungen:
- `HyperVProvider` — Microsoft Hyper-V über lokale PowerShell-Ausführung (die App läuft auf
  dem Hyper-V-Host, kein WinRM/Remoting).
- `DummyVirtualizationProvider` — In-Memory-Platzhalter für lokale Entwicklung.

Ein `ProxmoxProvider` wäre über dieselbe Schnittstelle umsetzbar. **Diese
Plattformunabhängigkeit ist der wissenschaftliche Kern der Arbeit** und der Grund für den
Interface-first-Entwurf.

## Bauen und Starten
```bash
dotnet build VmPortal.sln                                             # plattformübergreifend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project VmPortal.Api  # Dummy-Provider
ASPNETCORE_ENVIRONMENT=Production  dotnet run --project VmPortal.Api  # Hyper-V-Provider
```
Provider-Auswahl über `Virtualization:Provider` (`HyperV` | `Dummy`) in `appsettings.*.json`.

Die SQLite-Autorisierungs-DB (`vmportal.db`, nicht versioniert) muss vor dem ersten Start
per Migration angelegt werden — läuft **nicht** automatisch beim App-Start:
```bash
dotnet ef database update --project VmPortal.Core --startup-project VmPortal.Api
```
Details, Schema und Seed-Daten: [`docs/authorization.md`](docs/authorization.md).

Frontend:
```bash
cd VmPortal.Frontend && npm install && npm run dev   # Dev-Server auf :5173, proxyt /api
npm run build && cp -r dist/* ../VmPortal.Api/wwwroot/   # Produktions-Build in die API
```

## Testumgebung (nur Entwicklung — keine Produktions-Secrets)
- Windows Server 2022 als KVM-Gast auf Ubuntu, IP `192.168.122.196`, zugleich Hyper-V-Host.
- AD-Domäne `testumgebung.local`, LDAP auf Port 389.
- Testbenutzer: `mugur` / `Test1234!` und `jburath` / `Test1234!`
  (Gruppe `VM-Portal-Benutzer`, Rolle `VMUser`).
- Hyper-V-VMs: `VM-Mikail` (Notes = `mugur`), `VM-Burath` (Notes = `jburath`).

## Was bereits implementiert ist (M1–M5)
- **M1/M2:** Projektstruktur, Interfaces, Models, Dummy-Services, DI, testbare API.
- **M3:** LDAP-Authentifizierung gegen AD; Rolle aus AD-Gruppenmitgliedschaft; echtes JWT
  (HMAC-SHA256, Claims `username`/`role`, 8 h); JWT im `httpOnly`-Cookie
  (`SameSite=Strict`, `Secure`); JWT-Bearer-Middleware liest Token aus dem Cookie; alle
  Endpunkte außer `/api/auth/login` sind mit `[Authorize]` geschützt; RBAC im `VmController`
  (nur eigene VMs, `403` bei fremder VM).
- **M4:** `HyperVProvider` über lokale PowerShell-Ausführung (`Get-VM`, `Start-VM`,
  `Stop-VM -Force`, `Restart-VM -Force`, `Checkpoint-VM`) — kein WinRM/Remoting, da die App
  auf dem Hyper-V-Host läuft; konfigurierbare Provider-Auswahl;
  `VirtualizationExceptionMiddleware` liefert bei Hyper-V-Fehlern einen sprechenden `502`
  statt `500`; VM↔Benutzer-Zuordnung aus dem Hyper-V-Notizfeld; Logging aller Aktionen.
- **M5:** React-SPA (Vite, plain JS) mit Login-, VM-Übersichts- und VM-Detail-View
  (Status-Polling alle 5 s, Snapshot); Axios mit `withCredentials`, zentraler
  401-Interceptor, kein Auth-Token im Frontend-State/localStorage; kantiges Siemens-Design.
  Backend: CORS für den Vite-Dev-Server (`AllowCredentials`), Auslieferung des Builds aus
  `wwwroot` und SPA-Fallback (`MapFallbackToFile`).
- **M6 (Teil 1 — Autorisierungsschicht):** SQLite/EF Core als persistente
  Autorisierungsschicht neben AD (Hybrid: AD authentifiziert, SQLite autorisiert). RBAC mit
  fünf System-Rollen und frei erstellbaren Custom-Rollen über eine Rolle×Aktion-Matrix
  (`Roles`, `VMActions`, `RoleActions`), Rechtevergabe je AD-Gruppe × VM-Gruppe
  (`GroupPermissions`, Union aller zutreffenden Rollen statt „höchste Rolle gewinnt“),
  Bootstrap-FullAdmin über eine konfigurierbare AD-Gruppe. Admin-REST-Endpunkte
  (`/api/admin/roles|permissions|vm-groups|servers`) für Rollen-/Zuordnungsverwaltung.
- **M6 (Teil 2 — Umstellung `VmController`):** `VmController` prüft VM-Autorisierung jetzt
  ausschließlich über `DbAuthorizationService` (AD-Gruppen aus dem `adgroups`-Claim); die
  alte `vmroles`-Claim-Prüfung (`RolePermissions.IsAllowed` direkt im Controller) wurde
  entfernt. Der `vmroles`-Claim selbst wird unverändert weiter erzeugt (aktuell ohne
  Konsumenten, reserviert für mögliche Frontend-Anzeige). Verweigerungsgründe sind in den
  Logs unterscheidbar: „nicht authentifiziert“, „DB-Autorisierung verweigert (VM ohne
  Gruppe)“, „DB-Autorisierung verweigert (keine passende GroupPermission)“. Details:
  [`docs/authorization.md`](docs/authorization.md).

## Was noch kommt (Phase 6–7)
- **Phase 6 — Rest:** Audit-Log (wer hat wann welche VM-Aktion ausgeführt); Secrets aus
  `appsettings.json` in Umgebungsvariablen/Secret-Store auslagern; `deploy.ps1` (existiert
  noch nicht) um den `dotnet ef database update`-Schritt ergänzen; Testumgebung mit
  vollständigen `GroupPermissions` befüllen (nach der Umstellung auf
  `DbAuthorizationService` reicht eine leere/unvollständige Zuordnungstabelle nicht mehr
  aus, um dieselben Zugriffe wie vorher über `vmroles` zu erhalten).
- **Phase 7 — Evaluation:** Bewertung nach Sicherheit, Benutzerfreundlichkeit und
  Plattformunabhängigkeit; konzeptioneller Vergleich Hyper-V vs. Proxmox über das gemeinsame
  Interface.

## Wichtige Designentscheidungen (für die Bachelorarbeit relevant)
- **Interface-first (`IVirtualizationProvider`):** entkoppelt Portal-Kern vom Hypervisor und
  ermöglicht den Plattformvergleich, ohne die API zu ändern.
- **Provider-Auswahl per Konfiguration:** derselbe Code läuft mit `Dummy` (Linux-Entwicklung,
  automatisierbare Tests) oder `HyperV` (Windows-Produktion) — belegt die Austauschbarkeit
  praktisch.
- **JWT im `httpOnly`-Cookie statt `localStorage`:** Schutz gegen XSS-Token-Diebstahl;
  bewusste Sicherheitsentscheidung, die in der Arbeit begründet wird.
- **Rolle aus AD-Gruppe:** keine doppelte Benutzerverwaltung; das AD bleibt führendes System.
- **Fehlerübersetzung via Middleware:** Infrastrukturfehler (Hyper-V-Ausführung schlägt fehl)
  werden als `502` mit Klartextmeldung sichtbar, fachliche Fehler bleiben `403`/`404` — saubere
  Trennung der Fehlersemantik.
- **Cookie-Auth im SPA statt Token im Browser-Speicher:** Das Frontend hält kein JWT; es liegt
  nur im httpOnly-Cookie und wird per `withCredentials` mitgeschickt. Konsequente Fortführung
  der XSS-Härtung bis in die Client-Schicht.
- **Lokale PowerShell-Ausführung statt nativer Hyper-V-.NET-API:** Die Hyper-V-Cmdlets sind
  die offiziell unterstützte, stabile Automatisierungsschnittstelle. Da die App direkt auf dem
  Hyper-V-Host läuft, werden sie lokal ausgeführt — das umgeht die WinRM-Zugriffs- und
  Zertifikatsproblematik und benötigt keine Netzwerk-Zugangsdaten. Der Runspace wird mit
  `InitialSessionState.CreateDefault2()` erstellt, damit nur die Core-Cmdlets geladen werden
  und das Hyper-V-Modul bei Bedarf über den PSModulePath nachgeladen wird.
- **Hybrid-Autorisierung (AD authentifiziert, SQLite autorisiert):** Das AD bleibt alleinige
  Quelle für „wer ist der Nutzer und in welchen Gruppen ist er" — keine doppelte
  Benutzerverwaltung. Was eine AD-Gruppe auf welchen VMs darf, ist dagegen ein reines
  Anwendungskonzept ohne AD-Gegenstück und liegt daher lokal in SQLite/EF Core, verwaltbar
  über die Admin-UI statt über AD-Gruppenverschachtelung.
- **RBAC mit expliziten Rolle-Aktion-Mengen statt Level-Vererbung:** Anders als das ältere
  `VmRole`-Enum (Zahlenvergleich = Vererbung) definiert sich jede Rolle in der neuen Schicht
  über eine explizite, vollständige Aktionsliste (`RoleActions`). Grund: frei
  zusammenstellbare Custom-Rollen lassen sich nicht mehr in eine eindeutige Rangfolge
  bringen. Begründung ausführlich in `docs/authorization.md`.
- **Union statt „höchste Rolle gewinnt" bei mehreren zutreffenden Rollen:** Hat ein Nutzer
  über mehrere AD-Gruppen mehrere Rollen auf derselben VM-Gruppe, werden deren Aktionsmengen
  vereinigt statt nur die „höchste" zu nehmen — bei nicht-hierarchischen Custom-Rollen gibt es
  keine widerspruchsfreie Alternative dazu.

## Konventionen und Constraints
- ASP.NET Core 8, C#. Kein Blazor, kein Django, kein Python.
- Entwicklung unter Ubuntu, Ausführung auf Windows Server 2022 — Windows-APIs sind erlaubt,
  der Build muss aber plattformübergreifend fehlerfrei sein.
- Sämtliche Secrets/Verbindungsdaten in `appsettings.json`, nichts hartcodiert.
- Keine Breaking Changes an `IVirtualizationProvider`.
- Nach jeder abgeschlossenen Aufgabe committen (Conventional Commits, deutschsprachige
  Beschreibung); `dotnet build VmPortal.sln` muss grün sein.

## Bekannte Einschränkung beim Testen
Die Hyper-V-Cmdlets sind nur unter Windows verfügbar. Unter Linux baut und startet die App, der
`HyperV`-Provider quittiert einen Zugriff aber planmäßig mit `502` (`Get-VM` nicht bekannt). Ein
vollständiger Hyper-V-End-to-End-Test läuft daher auf dem Windows-Server-Deployment:
`GET /api/vm` liefert dort die realen VMs (`VM-Mikail`, `VM-Burath`), `start`/`stop` steuern sie.
