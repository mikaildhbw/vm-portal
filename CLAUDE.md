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
                  IDbAuthorizationService, IAdGroupSearchService
  Models/         VirtualMachine, VmStatus, VmRole, VmAction, VmReference
  Services/       HyperVProvider, DummyVirtualizationProvider,
                  LdapAuthService, DummyAuthService, JwtTokenService,
                  RolePermissions, VmRoleClaims, AdGroupClaims,
                  DbAuthorizationService, LdapAdGroupSearchService,
                  DummyAdGroupSearchService, VirtualizationException
  Configuration/  LdapSettings, JwtSettings, AuthorizationSettings,
                  TestVmRolesSettings, TestAdGroupsSettings, HyperVSettings
  Data/           VmPortalDbContext, AuthorizationSeedData, Entities/,
                  Migrations/ (SQLite-Autorisierungsschicht, siehe
                  docs/authorization.md)
VmPortal.Api/
  Controllers/    AuthController, VmController
  Controllers/Admin/ RolesController, PermissionsController,
                  VmGroupsController (inkl. VM-Gruppen-Mitgliederverwaltung),
                  ServersController, AdGroupsController, VmDiscoveryController
                  (alle FullAdmin-only)
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
- `HyperVProvider` — Microsoft Hyper-V, zwei Modi über `Virtualization:HyperV:Mode`:
  - `Local` — lokale PowerShell-Ausführung, die App läuft direkt auf dem Hyper-V-Host
    (ursprüngliches Verhalten, weiterhin unterstützt).
  - `Remote` — WinRM/Kerberos (Port 5985) gegen mehrere Hyper-V-Hosts, die App läuft auf
    einem separaten Server. **Implementiert und am 2026-08-19 auf der Produktions-VM
    erfolgreich gegen alle drei Ziel-Hosts (`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`)
    getestet** — pro Host ein wiederverwendbarer `RunspacePool`. Da VM-Namen über Hosts
    hinweg nicht eindeutig sind, identifiziert der Remote-Modus VMs über Host + Hyper-V-VM-GUID
    (`VirtualMachine.HostName`/`VmGuid`); die VM-Liste wird zusätzlich DB-first ermittelt
    (siehe M7 unten) statt das komplette Inventar aller Hosts zu scannen.
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
- Windows-Server-Testumgebung mit AD und Hyper-V (IP `192.168.122.196`), die die
  Produktionsbedingungen strukturell nachbildet; zugleich Hyper-V-Host.
- AD-Domäne `testumgebung.local`, LDAP auf Port 389.
- Testbenutzer: `mugur` / `Test1234!` und `jburath` / `Test1234!`
  (Gruppe `VM-Portal-Benutzer`, Rolle `VMUser`).
- Hyper-V-VMs: `VM-Mikail` (Notes = `mugur`), `VM-Burath` (Notes = `jburath`).

## Was bereits implementiert ist (M1–M8)
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
- **M7 (Produktivbetrieb — WinRM-Multi-Host, Login, Performance):** `HyperVProvider`
  unterstützt jetzt zusätzlich zum lokalen Modus einen WinRM/Kerberos-Remote-Modus
  (`Virtualization:HyperV:Mode=Remote`) für mehrere Hyper-V-Hosts — **am 2026-08-19 auf der
  Produktions-VM erfolgreich gegen alle drei Ziel-Hosts getestet:** `MHM-HYPERV1`,
  `MHM-HYPERV3`, `MHM-HYPERV4` (FQDN-Muster `<hostname>.archiv.mhm.siemens.com`;
  `MHM-VCLUSTER1` existiert **nicht** als eigenständiger Host, sondern ist eine zweite NIC
  von `MHM-HYPERV4`). Der frühere Verdacht, der Remote-Modus sei nur falsch konfiguriert
  gewesen, hat sich **nicht** bestätigt — er musste komplett neu gebaut werden (kein
  bloßer Config-Fix). Da VM-Namen über Hosts hinweg nicht eindeutig sind (bestätigte
  Kollisionen, z. B. `PLURI_DC1` identisch auf `MHM-HYPERV1` und `MHM-HYPERV3`),
  identifiziert der Remote-Modus VMs über Host + Hyper-V-VM-GUID statt über den Namen
  allein. Der Login gegen die Produktionsdomäne (`archiv.mhm.siemens.com`) funktioniert.
  Zusätzlich wurde die VM-Listen-/Autorisierungskette von einem Full-Inventory-Scan (alle
  VMs aller Hosts + Autorisierungsprüfung pro VM, N+1) auf DB-first umgestellt:
  `IDbAuthorizationService.GetAuthorizedVmsAsync` ermittelt die autorisierten
  (Host, VM)-Paare in einer einzigen Abfrage, danach fragt der Provider gezielt nur noch
  dafür beim jeweiligen Host nach. Bootstrap-FullAdmin (z. B. `ESX Admins`) bleibt
  Sonderfall mit vollem, ungefiltertem Inventarzugriff.
- **M8 (Admin-Backend für Rechtevergabe-Matrix, reines Backend, kein Frontend):** Drei neue
  FullAdmin-only-Endpunktgruppen als Vorbereitung für ein kommendes Admin-Panel
  (AD-Gruppe × VM-Gruppe × Rolle):
  - `GET /api/admin/ad-groups?search=` — durchsucht AD-Gruppen (nicht nur die des
    eingeloggten Nutzers) über `IAdGroupSearchService`; nutzt denselben
    LDAP-Verbindungsmechanismus wie `LdapAuthService`, aber einen eigenen Bind-Kontext
    (`Ldap:ServiceAccountUsername`/`ServiceAccountPassword`, sonst anonymer Bind-Versuch —
    **auf der Siemens-Produktions-AD vermutlich nicht ausreichend, Service-Account-Zugangsdaten
    müssen noch ergänzt werden**, siehe „Offene Punkte“). Ohne Suchbegriff auf 50, mit
    Suchbegriff auf 100 Treffer begrenzt (`truncated`-Flag statt Vollabruf).
  - `GET/POST/DELETE /api/admin/vm-groups/{groupId}/members` — VM-Gruppen-Mitgliederverwaltung
    (fehlte bisher komplett, `VmGroupsController` verwaltete nur die Gruppen selbst). POST legt
    einen `VirtualMachineRecord` bei Bedarf neu an (Host+Name, optional GUID) statt einen
    Fehler zu werfen; DELETE setzt `GroupId` auf `null` (secure-by-default), löscht den Eintrag
    nicht. Server-/Bestandsabgleich läuft je einmal pro Batch, nicht pro VM einzeln.
  - `GET /api/admin/discover-vms` — read-only Abgleich des vollen Hypervisor-Inventars (alle
    Hosts, wie bei Bootstrap-FullAdmin) gegen die DB; legt selbst nichts an. `VirtualServers`
    und `VirtualMachines` werden je einmal geladen (Dictionary/Lookup), kein Roundtrip pro VM.
  - Schema-Erweiterung: `VirtualMachineRecord.VmGuid` (nullable, additive Migration
    `AddVmGuidToVirtualMachines`) — dient nur der Nachvollziehbarkeit, **nicht** der
    Autorisierungsprüfung in `DbAuthorizationService` (die bleibt unverändert host-/namensbasiert).

## Was noch kommt (Phase 6–7)
- **Phase 6 — Rest:** Audit-Log (wer hat wann welche VM-Aktion ausgeführt); Secrets aus
  `appsettings.json` in Umgebungsvariablen/Secret-Store auslagern; Testumgebung mit
  vollständigen `GroupPermissions` befüllen (nach der Umstellung auf
  `DbAuthorizationService` reicht eine leere/unvollständige Zuordnungstabelle nicht mehr
  aus, um dieselben Zugriffe wie vorher über `vmroles` zu erhalten). `deploy.ps1` (inkl.
  `dotnet ef database update`-Schritt) existiert bereits im Repo.
- **M8 (Rest) — Admin-Panel-Frontend:** Bisher reines Backend (siehe M8 oben); UI für die
  Rechtevergabe-Matrix (AD-Gruppe × VM-Gruppe × Rolle, Autocomplete über
  `GET /api/admin/ad-groups`, VM-Auswahl über `GET /api/admin/discover-vms` +
  `POST .../members`) fehlt noch. Zusätzlich offen: `Ldap:ServiceAccountUsername`/
  `ServiceAccountPassword` müssen für die Produktions-AD (`archiv.mhm.siemens.com`) mit
  echten Service-Account-Zugangsdaten befüllt werden, sonst schlägt die AD-Gruppensuche dort
  vermutlich fehl (anonymer LDAP-Bind ist auf restriktiven ADs i. d. R. deaktiviert) — bisher
  nur gegen die lokale Testumgebung/den Dummy-Modus verifiziert.
- **Phase 7 — Evaluation:** Bewertung nach Sicherheit, Benutzerfreundlichkeit und
  Plattformunabhängigkeit; konzeptioneller Vergleich Hyper-V vs. Proxmox über das gemeinsame
  Interface.

## Wichtige Designentscheidungen (für die Bachelorarbeit relevant)
- **Interface-first (`IVirtualizationProvider`):** entkoppelt Portal-Kern vom Hypervisor und
  ermöglicht den Plattformvergleich, ohne die API zu ändern.
- **Provider-Auswahl per Konfiguration:** derselbe Code läuft mit `Dummy` (automatisierte
  Tests, infrastrukturunabhängige Entwicklung) oder `HyperV` (Windows-Produktion) — belegt
  die Austauschbarkeit praktisch.
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
  die offiziell unterstützte, stabile Automatisierungsschnittstelle. Läuft die App direkt auf
  dem Hyper-V-Host (`Mode=Local`), werden sie lokal ausgeführt — das umgeht die
  WinRM-Zugriffs- und Zertifikatsproblematik und benötigt keine Netzwerk-Zugangsdaten. Der
  Runspace wird mit `InitialSessionState.CreateDefault2()` erstellt, damit nur die
  Core-Cmdlets geladen werden und das Hyper-V-Modul bei Bedarf über den PSModulePath
  nachgeladen wird.
- **WinRM/Kerberos-Remote-Modus mit Host+GUID-Identifikation (`Mode=Remote`):** Läuft die App
  auf einem separaten Server (Produktivbetrieb), steuert sie mehrere Hyper-V-Hosts über
  PowerShell-Remoting an — pro Host ein wiederverwendbarer `RunspacePool` statt eines neuen
  Runspace-Aufbaus pro Aufruf, da WinRM-Verbindungsaufbau spürbar teurer ist als lokal. Da
  VM-Namen über Hosts hinweg nicht eindeutig sind, identifiziert dieser Modus VMs über die
  Kombination Host + Hyper-V-VM-GUID statt über den Namen allein.
- **DB-first statt Full-Inventory-Scan bei der VM-Liste:** `VmController.GetVms()` ermittelt
  zuerst per einziger DB-Abfrage, welche (Host, VM)-Paare der Nutzer sehen darf, und fragt den
  Hypervisor erst danach gezielt nur dafür an — statt das komplette Inventar aller Hosts zu
  holen und anschließend jede VM einzeln zu autorisieren (N+1-Problem). Bootstrap-FullAdmin
  bleibt Sonderfall mit vollem Inventarzugriff, da für ihn keine einschränkende
  `GroupPermission`-Zeile existiert, gegen die sich vorab filtern ließe.
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
- Der Build ist plattformübergreifend; die Ausführung mit dem HyperV-Provider erfordert
  Windows. Windows-APIs sind erlaubt, der Build muss aber plattformübergreifend fehlerfrei
  sein.
- Sämtliche Secrets/Verbindungsdaten in `appsettings.json`, nichts hartcodiert.
- Keine Breaking Changes an `IVirtualizationProvider`.
- Nach jeder abgeschlossenen Aufgabe committen (Conventional Commits, deutschsprachige
  Beschreibung); `dotnet build VmPortal.sln` muss grün sein.

## Dokumentationspflicht
Nach Abschluss jeder Aufgabe, die den **funktionalen Stand** des Projekts ändert (neue
Features, behobene Bugs, geänderte Architektur, korrigierte Fehlannahmen aus früheren
Sessions), aktualisiert Claude Code proaktiv `CLAUDE.md`, `README.md` und ggf.
`docs/PROJEKT_ERKLAERUNG.md` **im selben Durchgang** — nicht erst auf Nachfrage. Gilt
insbesondere für:
- Änderungen an der Architektur (neue Provider-Modi, neue Schichten),
- neue oder geänderte Konfigurationsschlüssel,
- geänderte Autorisierungslogik,
- behobene Fehleinschätzungen aus früheren Sessions (z. B. „X ist nur ein Config-Problem“
  stellt sich als „X muss neu gebaut werden“ heraus).

Reine Textkorrekturen ohne Funktionsänderung (Tippfehler, Formatierung) lösen das nicht aus.
Als letzten Schritt jeder abschließenden Zusammenfassung kurz benennen, welche
Dokumentationsdateien aktualisiert wurden (oder dass keine Aktualisierung nötig war und
warum) — damit das im Abschlussbericht sichtbar ist, statt separat nachgefragt werden zu
müssen.

## Bekannte Einschränkung beim Testen
Die Hyper-V-Cmdlets sind nur unter Windows verfügbar. Unter Linux baut und startet die App, der
`HyperV`-Provider quittiert einen Zugriff aber planmäßig mit `502` (`Get-VM` nicht bekannt). Ein
vollständiger Hyper-V-End-to-End-Test läuft daher auf Windows-Deployments:
- Lokaler Testumgebungs-Host (`Mode=Local`, IP `192.168.122.196`): `GET /api/vm` liefert die
  realen VMs (`VM-Mikail`, `VM-Burath`), `start`/`stop` steuern sie.
- Produktions-VM (`Mode=Remote`, separater Server): am 2026-08-19 erfolgreich gegen alle drei
  Ziel-Hosts (`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`) getestet, Login gegen
  `archiv.mhm.siemens.com` funktioniert.
