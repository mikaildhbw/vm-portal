# VmPortal — Projekterklärung (Onboarding für mich selbst)

> Ziel dieser Datei: Das gesamte Repo einmal komplett verstehen — was existiert,
> wo es liegt und **warum** es so gebaut wurde. Stand: 2026-08-12 (seit dem
> ursprünglichen Stand vom 2026-07-29, Commit `f4a70bc`, ist die
> SQLite/EF-Core-Autorisierungsschicht aus Kapitel 4.4 dazugekommen; Details siehe
> [`docs/authorization.md`](authorization.md)).
>
> **Update 2026-08-19:** WinRM-Multi-Host-Remote-Modus für `HyperVProvider`
> implementiert und auf der Produktions-VM erfolgreich gegen alle drei Ziel-Hosts
> (`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`) getestet (Details Abschnitt 2,
> "System.Management.Automation"). Login gegen die Produktionsdomäne
> (`archiv.mhm.siemens.com`) funktioniert. Die VM-Listen-/Autorisierungskette wurde
> von einem Full-Inventory-Scan mit Pro-VM-Prüfung (N+1) auf DB-first umgestellt
> (Details Abschnitt 5.1). Diese drei Punkte waren zuvor offene Baustellen
> (Abschnitt 7) und sind hiermit erledigt.

---

## 1. Überblick

**Was macht VmPortal?** VmPortal ist ein Self-Service-Portal für virtuelle Maschinen,
entwickelt im Rahmen der Bachelorarbeit bei der Siemens AG. Mitarbeiter melden sich mit
ihrem normalen Active-Directory-Konto an und können danach im Browser genau die VMs
sehen und steuern (starten, stoppen, Snapshots erstellen, …), für die sie im AD
berechtigt sind. Das löst das Problem, dass sonst für jede Kleinigkeit ("bitte VM
neu starten") ein Admin mit Zugriff auf den Hyper-V-Host bemüht werden muss —
Selbstbedienung statt Ticket, aber kontrolliert über Rollen.

**Forschungsfrage der Arbeit:** Wie wird ein sicheres Self-Service-Portal für virtuelle
Maschinen konzipiert, implementiert und hinsichtlich Sicherheit, Benutzerfreundlichkeit
und Plattformunabhängigkeit bewertet?

**Die drei Projekte und warum diese Aufteilung:**

| Projekt | Was drin ist | Rolle |
| --- | --- | --- |
| `VmPortal.Api` | Controller, Middleware, `Program.cs`, `appsettings` | Die REST-Schicht: nimmt HTTP-Anfragen an, prüft Authentifizierung/Autorisierung, übersetzt Fehler in HTTP-Statuscodes. Liefert im Produktivbetrieb außerdem das gebaute React-Frontend aus `wwwroot/` aus. |
| `VmPortal.Core` | Interfaces, Models, Services, Konfigurationsklassen, `Data/` (EF-Core-DbContext, Entities, Migrationen) | Die Fachlogik: alles, was das Portal *inhaltlich* kann (LDAP-Login, JWT-Erzeugung, Rollenmodell, Hypervisor-Ansteuerung, seit Kapitel 4.4 auch die SQLite-Autorisierungsschicht) — komplett ohne Wissen über HTTP. |
| `VmPortal.Frontend` | React-SPA (Vite, plain JavaScript) | Die Oberfläche: Login, VM-Übersicht, VM-Detail. Spricht ausschließlich über die REST-API mit dem Backend. |

**Warum die Trennung?** Die API-Schicht kennt nur Interfaces (`IVirtualizationProvider`,
`IAuthService`, `ITokenService`), nie konkrete Implementierungen. Dadurch kann man:

1. **den Hypervisor austauschen** (Hyper-V ↔ Dummy ↔ konzeptionell Proxmox), ohne eine
   Zeile im Controller zu ändern — das ist der wissenschaftliche Kern der Arbeit
   (Plattformunabhängigkeit),
2. **plattformunabhängig entwickeln**, obwohl Hyper-V nur auf Windows existiert (Dummy-Provider),
3. die Fachlogik theoretisch auch aus einem anderen Host (CLI, Tests) heraus nutzen.

Das Frontend ist ein eigenes Projekt, weil es eine eigene Toolchain hat (npm/Vite) und
als statisches Build-Artefakt (`dist/`) in die API kopiert wird — es ist zur Laufzeit
nur "totes" HTML/JS, alle Logik mit Sicherheitsrelevanz liegt im Backend.

---

## 2. Technologie-Stack, Baustein für Baustein

### ASP.NET Core 8 (Backend-Framework)
Microsofts aktuelles Web-Framework für .NET: HTTP-Server (Kestrel), Routing,
Dependency Injection, Middleware-Pipeline — alles eingebaut. **Warum:** Vorgabe/
Constraint des Projekts (C#, kein Python/Django, kein Blazor), läuft plattformübergreifend
(Entwicklung plattformunabhängig, Betrieb auf Windows Server 2022), und die
Windows-Nähe ist ein Plus, weil das Ziel-System ein Hyper-V-Host ist. .NET 8 ist
eine LTS-Version (Long-Term Support).

### React 18 + Vite (Frontend)
React rendert die Oberfläche als Single-Page-Application (SPA); Vite ist das
Build-Tool, das im Dev-Modus einen schnellen Dev-Server mit Hot-Reload bereitstellt
und für Produktion ein statisches Bundle (`dist/`) baut. **Warum React statt z. B.
Blazor:** Blazor war explizit ausgeschlossen; React ist der Industriestandard und
für die Arbeit als "typischer" SPA-Stack gut begründbar. **Warum Vite statt
Create-React-App:** CRA ist faktisch tot (deprecated), Vite ist der aktuelle
Standard und deutlich schneller. **Warum plain JavaScript statt TypeScript:**
bewusst schlank gehalten — das Frontend hat kaum Logik (drei Views, ein API-Client),
die ganze sicherheitsrelevante Logik liegt im typisierten C#-Backend.

### Axios + React Router
Axios ist der HTTP-Client des Frontends. Entscheidend ist die zentrale Instanz in
`VmPortal.Frontend/src/api/client.js` mit `withCredentials: true` (schickt das
httpOnly-Cookie automatisch mit) und einem Response-Interceptor, der bei `401`
zentral zur Login-Seite umleitet — so muss keine einzelne Komponente
Session-Ablauf behandeln. React Router (v7) übernimmt das clientseitige Routing
(`/`, `/vms`, `/vms/:id`).

### JWT — JSON Web Token (`System.IdentityModel.Tokens.Jwt` + `Microsoft.AspNetCore.Authentication.JwtBearer`)
Ein JWT ist ein signierter Token, der Aussagen über den Benutzer ("Claims") enthält:
wer er ist, welche Rollen er hat, wie lange der Token gilt. Signiert wird hier
symmetrisch mit HMAC-SHA256 und einem Secret aus `appsettings.json`.
**Warum JWT statt Server-Session:** Das Backend braucht keinen Session-Speicher —
alle Informationen (Benutzername, VM-Rollen) stecken im Token selbst, jede Anfrage
ist selbsterklärend ("stateless"). Der Preis dafür: Ein einmal ausgestellter Token
ist bis zu seinem Ablauf gültig und kann nicht serverseitig widerrufen werden
(siehe Kapitel 7).

### JWT im httpOnly-Cookie statt localStorage
Der Token wird **nicht** im JavaScript-Zugriff gehalten (kein `localStorage`, kein
State), sondern vom Server als Cookie gesetzt mit:
- `HttpOnly` → JavaScript kann das Cookie nicht lesen → ein XSS-Angriff (eingeschleustes
  Skript) kann den Token nicht stehlen,
- `SameSite=Strict` → der Browser schickt das Cookie nur bei Anfragen von der eigenen
  Seite mit → Schutz gegen CSRF (untergeschobene Anfragen von fremden Seiten),
- `Secure` → Cookie wird nur über HTTPS übertragen (Ausnahme: `localhost`).

**Warum:** `localStorage` ist die häufigste, aber unsicherste Ablage — jedes im
Frontend laufende Skript kann ihn auslesen. Das httpOnly-Cookie ist die bewusste
Sicherheitsentscheidung, die in der Arbeit begründet wird. Die Kehrseite (Cookie
wird automatisch mitgeschickt → CSRF-Risiko) wird durch `SameSite=Strict` adressiert.

### Novell.Directory.Ldap.NETStandard (LDAP-Client)
Eine plattformunabhängige .NET-Bibliothek für das LDAP-Protokoll, mit der sich das
Backend am Active Directory anmeldet (Bind) und Gruppenmitgliedschaften abfragt.
**Warum nicht `System.DirectoryServices` (Microsofts eigene AD-API):** Die ist
Windows-only — der Build muss aber plattformübergreifend grün sein. Die Novell-Bibliothek
spricht rohes LDAP und läuft überall. (Der Name ist historisch — das ist eine Portierung der alten Novell/OpenLDAP-
Java-Bibliothek, heute community-gepflegt.)

### System.Management.Automation (PowerShell-Ausführung in-process)
Das NuGet-Paket, das die PowerShell-Engine als Bibliothek in den .NET-Prozess holt.
`HyperVProvider` erzeugt damit einen Runspace und ruft die offiziellen Hyper-V-Cmdlets
(`Get-VM`, `Start-VM`, …) direkt im eigenen Prozess auf — kein externer
`powershell.exe`-Aufruf, kein Parsen von Text-Output, sondern echte .NET-Objekte
(`PSObject`) als Ergebnis.

**Warum PowerShell statt einer nativen Hyper-V-.NET-API:** Es gibt keine offizielle,
unterstützte Hyper-V-.NET-Bibliothek. Die Alternativen wären:
- **WMI/CIM direkt** (`root\virtualization\v2`): funktioniert, ist aber extrem
  low-level, schlecht dokumentiert und fehleranfällig (man baut faktisch nach, was
  die Cmdlets intern tun).
- **PowerShell-Remoting über WinRM:** war die erste Implementierung (Commit `085727e`),
  wurde aber bewusst wieder entfernt (Commit `614c398`), weil die App zu diesem
  Zeitpunkt ausschließlich **direkt auf dem Hyper-V-Host lief** — lokale Ausführung
  umgeht die komplette WinRM-Zertifikats- und Berechtigungsproblematik und braucht
  keinerlei Netzwerk-Zugangsdaten in der Konfiguration.

**Update 2026-08-19 — WinRM ist zurück, diesmal als produktiver Multi-Host-Modus:**
Der Produktivbetrieb läuft jetzt auf einem separaten Server, der drei Hyper-V-Hosts
ansteuert — ein einzelner impliziter lokaler Host reicht dafür nicht mehr aus.
`HyperVProvider` unterstützt seitdem zusätzlich einen `Remote`-Modus
(`Virtualization:HyperV:Mode`): pro Host ein wiederverwendbarer `RunspacePool` über
`WSManConnectionInfo` (Kerberos, Port 5985, Authentifizierung über die
Prozessidentität, kein Credential in der Konfiguration). Wichtig für die
Bachelorarbeit: Ein früherer Verdacht in dieser Session war, der ursprünglich
entfernte Remote-Modus sei nur falsch konfiguriert gewesen und ließe sich mit
wenig Aufwand reaktivieren — das hat sich **nicht bestätigt**. Es musste komplett
neu gebaut werden, u. a. weil VM-Namen über die drei Hosts hinweg nicht eindeutig
sind (bestätigte Kollisionen, siehe Abschnitt 4 bzw. die Migration
`FixVirtualServersHostCount`) und der ursprüngliche Code dafür keine Lösung hatte
(Identifikation nur über den VM-Namen). Der neue Modus identifiziert VMs daher über
Host + Hyper-V-VM-GUID. Am 2026-08-19 erfolgreich gegen alle drei Ziel-Hosts
(`MHM-HYPERV1`, `MHM-HYPERV3`, `MHM-HYPERV4`) getestet, inklusive Login gegen die
Produktionsdomäne `archiv.mhm.siemens.com`.

Die Hyper-V-Cmdlets sind Microsofts offiziell unterstützte, stabile
Automatisierungsschnittstelle — genau das, was auch ein Admin von Hand benutzen würde.
Detail in `HyperVProvider.InvokeAsync()`: Der Runspace wird mit
`InitialSessionState.CreateDefault2()` erstellt — das lädt nur die Core-Cmdlets und
vermeidet die Abhängigkeit zum Konsolen-Host; das Hyper-V-Modul wird unter Windows
bei Bedarf automatisch über den `PSModulePath` nachgeladen. Unter Linux existiert
das Modul nicht → `Get-VM` unbekannt → sauberer `502` (planmäßig, siehe Kapitel 7).

### Entity Framework Core + SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)
EF Core ist Microsofts ORM (Object-Relational Mapper) für .NET; SQLite ist eine
dateibasierte, serverlose Datenbank (eine einzelne Datei `vmportal.db`, kein separater
Datenbankprozess). Trägt seit Kapitel 4.4 die neue Autorisierungsschicht (Rollen,
VM-Gruppen, AD-Gruppen-Zuordnungen). **Warum SQLite statt PostgreSQL/SQL Server:** Die
Autorisierungsdaten sind klein (ein paar hundert Zeilen, kein Multi-User-Concurrent-Write
in nennenswertem Umfang) und die App läuft ohnehin nur auf genau einem Host (dem
Hyper-V-Server) — ein separater DB-Server wäre reiner Betriebs-Overhead ohne Nutzen. Der
Preis: SQLite ist nicht für hohe Nebenläufigkeit ausgelegt, was hier unkritisch ist.
**Warum EF Core statt Dapper/rohem ADO.NET:** Migrationen (`dotnet ef migrations add`)
versionieren das Schema nachvollziehbar mit, `HasData` seedet deterministisch beim
`database update` — für eine Bachelorarbeit mit reproduzierbarem Deployment wichtiger als
die letzten Prozent ORM-Overhead.

### Dependency Injection (eingebaut in ASP.NET Core)
In `Program.cs` wird pro Interface genau eine Implementierung registriert — welche,
entscheidet die Konfiguration (`Virtualization:Provider`, `Auth:Provider`). Controller
bekommen die Interfaces per Konstruktor. Das ist der Mechanismus, der die
Austauschbarkeit (Dummy ↔ HyperV, Dummy ↔ Ldap) ohne Codeänderung praktisch möglich macht.

---

## 3. Architektur und Datenfluss: "Nutzer klickt Start"

Die vier Schichten sind: **Frontend (React)** → **REST-API (Controller)** →
**Fachlogik/Provider (Core)** → **Hypervisor (Hyper-V via PowerShell)**.
Hier der komplette Durchlauf, wenn ein angemeldeter Nutzer in der Detailansicht
auf **Start** klickt:

1. **`VmPortal.Frontend/src/pages/VmDetail.jsx`** — der Start-Button ruft
   `runAction(startVm, 'Start')` auf; das setzt `busy` (Buttons gesperrt) und ruft
   die API-Funktion.

2. **`VmPortal.Frontend/src/api/vmApi.js`** — `startVm(name)` macht
   `client.post('/vm/VM-Mikail/start')`.

3. **`VmPortal.Frontend/src/api/client.js`** — die zentrale Axios-Instanz hängt
   `baseURL: '/api'` davor und schickt wegen `withCredentials: true` das
   httpOnly-JWT-Cookie automatisch mit. Im Dev-Betrieb leitet der Vite-Proxy
   (`vite.config.js`) die Anfrage an `http://192.168.122.196:5000` weiter; in
   Produktion läuft das Frontend eh unter derselben Origin wie die API.

4. **`VmPortal.Api/Program.cs` — die Middleware-Pipeline** (Reihenfolge ist wichtig):
   1. `VirtualizationExceptionMiddleware` legt sich als äußerster try/catch um alles,
   2. HTTPS-Redirect, statische Dateien (greifen hier nicht, da `/api/...`),
   3. CORS,
   4. **Authentication:** Die JWT-Bearer-Middleware feuert das selbstgebaute
      `OnMessageReceived`-Event (Program.cs:57–65), das den Token **aus dem Cookie
      `jwt`** liest statt aus dem `Authorization`-Header. Danach validiert sie
      Signatur, Issuer, Audience und Ablaufzeit. Gültig → `User` (ClaimsPrincipal)
      ist gefüllt; ungültig/fehlend → die Anfrage ist anonym.
   5. **Authorization:** `[Authorize]` am Controller lehnt anonyme Anfragen mit `401` ab.

5. **`VmPortal.Api/Controllers/VmController.cs`** — Route `POST api/vm/{id}/start`
   trifft `StartVm(id)`, das an `ExecuteVmActionAsync(id, VmAction.Start, …)` delegiert.
   Dort passiert die Autorisierung in `AuthorizeVmActionAsync` (VmController.cs:164):
   1. `_virtualizationProvider.GetVmByIdAsync(id)` — existiert die VM überhaupt?
      Nein → `404`.
   2. `GetVmRolesFromToken()` liest alle `vmroles`-Claims aus dem Token und baut
      daraus per `VmRoleClaims.Deserialize` das Dictionary VM-Name → Rolle.
   3. `RolePermissions.IsAllowed(role, VmAction.Start)` — hat der Nutzer für
      **diese** VM mindestens die Rolle `Operator`? Nein (oder gar keine Rolle für
      die VM) → `403 Forbid`.

6. **`VmPortal.Core/Services/HyperVProvider.cs`** — `StartVmAsync(id)` ruft
   `InvokeAsync(ps => ps.AddCommand("Start-VM").AddParameter("Name", id))` auf:
   Runspace öffnen, `Start-VM -Name VM-Mikail` in-process ausführen, bei
   PowerShell-Fehlern eine `VirtualizationException` mit der ersten Fehlermeldung
   werfen. (Parameter werden über `AddParameter` übergeben, nicht in einen
   Befehlsstring interpoliert — das verhindert Command-Injection.)

7. **Hyper-V** startet die VM. Der Controller antwortet `200 OK` (leer).

8. **Fehlerpfad:** Wirft der Provider eine `VirtualizationException` (z. B. Hyper-V
   nicht erreichbar), fängt die `VirtualizationExceptionMiddleware`
   (`VmPortal.Api/Middleware/VirtualizationExceptionMiddleware.cs`) sie und antwortet
   `502 Bad Gateway` mit der Klartextmeldung; eine `NotImplementedException`
   (bewusst nicht umgesetzte Aktion) wird zu `501 Not Implemented`. So bleibt die
   Fehlersemantik sauber getrennt: `401` nicht angemeldet, `403` keine Berechtigung,
   `404` VM unbekannt, `501` Feature bewusst nicht umgesetzt, `502` Infrastrukturfehler.

9. **Zurück im Frontend:** `runAction` zeigt "Start ausgelöst." und lädt den
   VM-Zustand neu; unabhängig davon pollt `VmDetail` alle 5 Sekunden
   (`POLL_INTERVAL_MS`) den Status, sodass der Statuswechsel `Gestoppt → Läuft`
   sichtbar wird.

---

## 4. Authentifizierung und Autorisierung im Detail

### 4.1 Was beim Login passiert

Einstiegspunkt: `POST /api/auth/login` → `AuthController.Login` (mit
`[AllowAnonymous]`, der einzige offene Endpunkt) → `IAuthService.LoginAsync`.
Im LDAP-Modus (`Auth:Provider = "Ldap"`) läuft in
`VmPortal.Core/Services/LdapAuthService.cs`:

1. **Verbindung:** `LdapConnection` zu `Ldap:Host` : `Ldap:Port`
   (Testumgebung: `192.168.122.196:389`, unverschlüsselt —
   `SecureSocketLayer = false`, siehe Kapitel 7).

2. **LDAP-Bind = Passwortprüfung:** `conn.BindAsync("mugur@testumgebung.local", password)`.
   Der UPN (`benutzer@domäne`) wird aus dem `BaseDn` gebaut, indem
   `DC=testumgebung,DC=local` zu `testumgebung.local` umgeformt wird. Der Trick:
   Das Portal prüft das Passwort **nie selbst** — es versucht schlicht, sich *als
   dieser Benutzer* am AD anzumelden. Gelingt der Bind, war das Passwort richtig;
   scheitert er, wirft die Bibliothek eine `LdapException` → `401` mit Fehlermeldung.
   Es gibt keine eigene Benutzer-/Passwortverwaltung, das AD bleibt führendes System.

3. **Gruppen laden:** Mit der (nun authentifizierten) Verbindung wird nach
   `(sAMAccountName=mugur)` gesucht und das Attribut `memberOf` gelesen — die Liste
   der Gruppen-DNs, z. B. `CN=VM-Mikail-PowerUser,OU=Gruppen,DC=testumgebung,DC=local`.
   Aus jedem DN wird nur der CN (der Gruppenname) extrahiert.
   Wichtig: `memberOf` liefert nur **direkte** Mitgliedschaften — verschachtelte
   Gruppen (AGDLP) werden aktuell nicht aufgelöst (siehe Kapitel 7).

4. **Globale Rolle:** Ist der Nutzer in der Gruppe `VM-Portal-Benutzer`, bekommt er
   die Rolle `VMUser`, sonst `User`. Das ist ein Legacy-Claim aus M3 — die
   eigentliche Autorisierung läuft inzwischen über die VM-Rollen.

5. **VM-Rollen extrahieren** (`ExtractVmRoles`): Jeder Gruppenname wird gegen das
   Muster `VM-{VmName}-{Rolle}` geprüft (Regex
   `^(?<vm>VM-.+)-(?<role>Viewer|Operator|PowerUser|Admin|FullAdmin)$`).
   `VM-Mikail-PowerUser` → VM `VM-Mikail`, Rolle `PowerUser`. Ist ein Nutzer in
   mehreren Rollengruppen derselben VM, gewinnt die höchste Rolle.

6. **JWT bauen** (`VmPortal.Core/Services/JwtTokenService.cs`): Claims sind
   `sub`/`name` (Benutzername), `role` (globale Rolle), `jti` (eindeutige Token-ID)
   und — der wichtige — **`vmroles`**: die VM→Rolle-Zuordnung als JSON-Array, z. B.
   `[{"vm":"VM-Mikail","role":"PowerUser"}]` (Serialisierung in
   `VmPortal.Core/Services/VmRoleClaims.cs`). Signiert mit HMAC-SHA256 über
   `Jwt:Secret`, gültig `Jwt:ExpiryHours` (8 h).

7. **Cookie setzen:** Der `AuthController` legt den Token in das Cookie `jwt`
   (`HttpOnly`, `Secure`, `SameSite=Strict`, Ablauf 8 h — hier hart codiert,
   unabhängig von `ExpiryHours`, siehe Kapitel 7) und antwortet
   `200 {"message":"Login erfolgreich"}`. Der Token taucht **nie** im Response-Body
   auf — das Frontend sieht ihn schlicht nicht.

Im Dummy-Modus (`Auth:Provider = "Dummy"`, `DummyAuthService`) passiert dasselbe,
nur ohne AD: feste Testnutzer `mugur`/`jburath` mit Passwort `Test1234!`, VM-Rollen
aus dem Konfigurationsabschnitt `TestVmRoles` statt aus AD-Gruppen. Der Rest der
Pipeline (JWT, Cookie, Autorisierung) ist identisch — genau deshalb existiert der
Dummy: die komplette Auth-Kette ist infrastrukturunabhängig, ohne echtes AD, testbar.

### 4.2 Was bei jeder folgenden Anfrage passiert

1. Der Browser schickt das Cookie `jwt` automatisch mit (Axios: `withCredentials`).
2. Die JWT-Bearer-Middleware zieht den Token per `OnMessageReceived` aus dem Cookie
   und validiert Signatur, Issuer, Audience, Ablaufzeit. Alles serverseitig, ohne
   LDAP-Kontakt — das AD wird nur beim Login gebraucht.
3. `[Authorize]` am Controller: kein gültiger Token → `401` (Frontend-Interceptor
   leitet zum Login um).
4. Im `VmController` wird pro Aktion die VM-Rolle geprüft (siehe 4.3).

Ein Detail, das einen echten Bug verursacht hatte (Fix in Commit `f4a70bc`): Der
ASP.NET-JWT-Handler zerlegt einen JSON-**Array**-Claim beim Validieren in **einen
Claim pro Array-Element**. Deshalb liest `GetVmRolesFromToken()` mit
`User.FindAll(...)` (alle Claims namens `vmroles`) statt `FindFirst`, und
`VmRoleClaims.Deserialize` akzeptiert sowohl die Array- als auch die
Einzelobjekt-Form. Ein syntaktisch kaputter Claim wird bewusst wie "keine Rolle"
behandelt — ein defekter Claim darf keine Rechte verleihen (fail-closed).

### 4.3 RBAC konkret: Rollen, Aktionen, ein durchgerechnetes Beispiel

Drei Bausteine in `VmPortal.Core`:

- **`Models/VmRole.cs`** — die Rollenhierarchie als Enum mit aufsteigenden Zahlen:
  `Viewer = 0` < `Operator = 1` < `PowerUser = 2` < `Admin = 3` < `FullAdmin = 4`.
  Der Zahlenvergleich **ist** die Vererbung: eine höhere Rolle darf alles, was die
  niedrigeren dürfen.
- **`Models/VmAction.cs`** — der Katalog aller 22 autorisierbaren Aktionen
  (ViewStatus … LiveMigrate).
- **`Services/RolePermissions.cs`** — die statische Tabelle "Aktion → mindestens
  benötigte Rolle":

  | Mindestrolle | Aktionen |
  | --- | --- |
  | Viewer | ViewStatus, ViewDetails, ViewMetering |
  | Operator | Start, Stop, Pause, Resume, SaveState |
  | PowerUser | Reset, SnapshotCreate, SnapshotApply, ConsoleConnect |
  | Admin | SnapshotDelete, ResizeRam, ResizeCpu, AttachNetworkAdapter, VhdResize, VhdCompact |
  | FullAdmin | Export, Import, Clone, LiveMigrate |

  Die Prüfung ist eine Zeile: `IsAllowed(userRole, action) => userRole >= MinimumRoleFor(action)`.

**Durchgerechnetes Beispiel — `mugur` versucht, RAM zu ändern:**

`mugur` ist im AD in der Gruppe `VM-Mikail-PowerUser` → sein JWT enthält
`vmroles: [{"vm":"VM-Mikail","role":"PowerUser"}]`. Er ruft
`POST /api/vm/VM-Mikail/resize-ram` mit Body `4096` auf.

1. Cookie ist gültig → Anfrage kommt authentifiziert im `VmController` an
   (`ResizeRam` → `ExecuteVmActionAsync(id, VmAction.ResizeRam, …)`).
2. `AuthorizeVmActionAsync` (VmController.cs:164): `GetVmByIdAsync("VM-Mikail")`
   findet die VM → kein `404`.
3. `GetVmRolesFromToken()` liefert `{ "VM-Mikail" → PowerUser }`. Der Lookup mit
   `vm.Name` klappt → `role = PowerUser (2)`.
4. `RolePermissions.IsAllowed(PowerUser, ResizeRam)`: Die Tabelle sagt
   `ResizeRam → Admin (3)`. Prüfung: `2 >= 3` → **false**.
5. VmController.cs:172 → `Forbid()` → **`403 Forbidden`**. Der `HyperVProvider`
   wird nie aufgerufen — die Ablehnung passiert komplett in der API-Schicht.

Zum Vergleich: `POST /api/vm/VM-Mikail/snapshot` (SnapshotCreate → PowerUser,
`2 >= 2`) geht durch. Und für `VM-Burath` hat `mugur` **gar keinen** Eintrag im
Dictionary → `TryGetValue` schlägt fehl → ebenfalls `403` — implizite
Nicht-Berechtigung: was nicht ausdrücklich erlaubt ist, ist verboten. Auch in der
Übersicht (`GET /api/vm`) taucht `VM-Burath` für ihn nicht auf, weil dort nach
"hat mindestens Viewer" gefiltert wird.

### 4.4 Die neue SQLite-Autorisierungsschicht (parallel zum `vmroles`-Pfad)

Seit dem 2026-08-12-Stand gibt es **zusätzlich** zur eben beschriebenen
`vmroles`-Autorisierung eine zweite, persistente Autorisierungsschicht in SQLite/EF Core
(`VmPortal.Core/Data/`). Wichtig zum Verständnis: **beide Pfade existieren aktuell
nebeneinander.** `VmController` prüft nach wie vor ausschließlich über den
`vmroles`-Claim (Kapitel 4.3) — die neue Schicht ist noch nicht in ihn verdrahtet. Sie
liefert bislang nur die Grundlage (Schema, Autorisierungslogik, Admin-API), über die
Rollen und AD-Gruppen-Zuordnungen verwaltbar sind, ohne AD-Gruppen manuell nach dem
`VM-{VmName}-{Rolle}`-Schema anlegen zu müssen. Die Umstellung von `VmController` auf
diese Schicht ist offener Punkt (Kapitel 7, Punkt 8).

**Warum überhaupt eine zweite Schicht, wenn `vmroles` schon funktioniert?** Der
`vmroles`-Ansatz kodiert die komplette Rechtevergabe implizit in AD-Gruppennamen
(`VM-Mikail-PowerUser`) — jede neue Zuordnung braucht eine neue AD-Gruppe, jede VM einen
eigenen Satz von fünf Rollengruppen, und die Rollen selbst (`Viewer`…`FullAdmin`) sind im
Code hart verdrahtet, nicht administrierbar. Die SQLite-Schicht trennt das: AD liefert nur
noch *Gruppenmitgliedschaft* (roher Gruppenname, kein Rollen-Parsing), alles Weitere —
welche Rolle welche Aktionen darf, welche AD-Gruppe welche Rolle auf welcher VM-Gruppe hat —
liegt in der DB und ist über eine Admin-UI pflegbar, inklusive frei erstellbarer
Custom-Rollen (nicht nur der fünf fest kodierten).

**Kernstücke** (ausführlich in [`docs/authorization.md`](authorization.md)):

- **Schema** (`VmPortalDbContext`, `Data/Entities/`): `VirtualServers`,
  `VirtualMachines` (Entity-Klasse heißt `VirtualMachineRecord` — Namenskollision mit dem
  Laufzeit-Modell `Models.VirtualMachine` des Providers vermieden, EF-Tabellenname bleibt
  aber `VirtualMachines`), `VirtualMachineGroups`, `UserGroups` (1:1-Abbild einer
  AD-Gruppe, nur Name, kein Sync), `Roles`, `VMActions` (Entity `VmActionEntity`, gleiches
  Namenskollisions-Problem mit dem `VmAction`-Enum), `RoleActions`, `GroupPermissions`.
- **RBAC statt Level-Vererbung:** Jede Rolle definiert sich über ihre `RoleActions` —
  eine explizite, vollständige Aktionsliste, keine implizite Vererbung über `Roles.Level`
  (das dient nur noch der UI-Sortierung). Grund: frei zusammenstellbare Custom-Rollen
  lassen sich nicht mehr eindeutig in eine Rangfolge bringen (Details/Beispiel in
  `docs/authorization.md`).
- **Bootstrap-FullAdmin:** Ist eine AD-Gruppe des Nutzers gleich
  `Authorization:BootstrapFullAdminGroup` (`VM-Portal-Benutzer` lokal, `ESX Admins` in
  Produktion — bewusst dieselbe lokale Testgruppe wie der `VMUser`-Legacy-Claim aus 4.1),
  gilt er ohne DB-Abfrage als FullAdmin mit allen 22 Aktionen. Das ist der einzige Weg, wie
  nach einem frischen Deployment (leere `GroupPermissions`) überhaupt jemand über die
  Admin-API erste Rollen/Zuordnungen anlegen kann.
- **`DbAuthorizationService.GetAllowedActionsAsync(adGroups, vmName)`:** Bootstrap-Check →
  VM-Gruppe auflösen (keine Gruppe = keine Rechte, secure-by-default) → alle
  `GroupPermissions` finden, deren `UserGroupId` einer AD-Gruppe des Nutzers entspricht →
  **Union** der `RoleActions` aller zutreffenden Rollen zurückgeben. Union statt "höchste
  Rolle gewinnt", weil Custom-Rollen keine totale Ordnung mehr haben — siehe die
  ausführliche Herleitung in `docs/authorization.md` (das ist der Teil, der auch in der
  Bachelorarbeit argumentativ gebraucht wird).
- **Neuer JWT-Claim `adgroups`:** Trägt die rohen AD-Gruppennamen (dieselbe Quelle wie
  `memberOf` in Kapitel 4.1, Schritt 3 — nur diesmal *ohne* das Herausparsen von
  VM-Name/Rolle). Existiert **zusätzlich** zum `vmroles`-Claim, der unverändert bleibt
  (`VmPortal.Core/Services/AdGroupClaims.cs`). Im Dummy-Modus liefert der neue
  Konfigurationsabschnitt `TestAdGroups` (Pendant zu `TestVmRoles`, Kapitel 6) simulierte
  Gruppenmitgliedschaften.
- **Admin-REST-API** (`VmPortal.Api/Controllers/Admin/*`, alle über
  `AdminControllerBase` mit `[Authorize]` + Bootstrap-FullAdmin-Prüfung als
  `IActionFilter`): `RolesController` (CRUD für Custom-Rollen, System-Rollen sind
  schreibgeschützt), `PermissionsController` (`GroupPermissions`-Zuordnungen),
  `VmGroupsController`, `ServersController`.
- **Migration `InitialAuthorizationSchema`** (`Data/Migrations/`) legt Schema **und**
  Seed-Daten per `HasData` an (fünf System-Rollen mit den aus `RolePermissions`
  übernommenen `RoleActions`, alle 22 `VMActions`, die vier Hyper-V-Hosts, die beiden
  Bootstrap-`UserGroups`) — läuft **nicht** automatisch beim App-Start, muss separat per
  `dotnet ef database update` ausgeführt werden (kein `deploy.ps1` im Repo, das diesen
  Schritt bislang automatisiert). **Update 2026-08-19:** Von den ursprünglich vier
  Seed-Einträgen waren nur drei reale, eigenständige Hyper-V-Hosts — `MHM-HYPERV1`,
  `MHM-HYPERV3`, `MHM-HYPERV4` (FQDN `<hostname>.archiv.mhm.siemens.com`); der vierte
  Eintrag `MHM-VCLUSTER1` bezeichnete keinen eigenen Host, sondern eine zweite NIC von
  `MHM-HYPERV4`. Zwei Folgemigrationen haben das inzwischen korrigiert bzw. ergänzt:
  `FixVirtualServersHostCount` entfernt den `MHM-VCLUSTER1`-Eintrag,
  `SeedTestUserPermissions` seedet eine Testberechtigung (UserGroup `ESXUserIT`,
  VM-Gruppe `Testumgebung-HVP`, Rolle PowerUser) für die neun Hyper-V-Test-VMs
  `HVP_1`–`HVP_9` auf `MHM-HYPERV4`.

---

## 5. Wirklich implementiert vs. vorbereitet/Platzhalter

### 5.1 HyperVProvider — was ruft echte Cmdlets auf

Alle folgenden Methoden führen echte PowerShell-Cmdlets aus — im `Local`-Modus lokal
in-process (wie ursprünglich), im `Remote`-Modus über den `RunspacePool` des jeweiligen
Hosts (WinRM/Kerberos, siehe Abschnitt 2, Update 2026-08-19):

| Methode | Cmdlet | Anmerkung |
| --- | --- | --- |
| `GetVmsAsync()` | `Get-VM` (pro Host) | ungefiltertes volles Inventar — nur für Bootstrap-FullAdmin bzw. Admin-Kontexte |
| `GetVmsAsync(authorizedVms)` | `Get-VM -Name <Array>` (pro betroffenem Host) | **neu, Performance-Fix:** fragt gezielt nur die per DB-Autorisierung ermittelten VMs ab statt des kompletten Inventars; Hosts ohne autorisierte VMs werden nicht angefragt |
| `GetVmByIdAsync` | `Local`: `Get-VM -Name {id}` · `Remote`: `Get-VM -Id {guid}` auf dem aus der Id geparsten Host | `Id` ist im `Local`-Modus der VM-Name (wie ursprünglich), im `Remote`-Modus `Host::Guid` — VM-Namen sind über Hosts hinweg nicht eindeutig |
| `StartVmAsync` | `Start-VM` | |
| `StopVmAsync` | `Stop-VM -Force` | |
| `ResetVmAsync` | `Restart-VM -Force` | |
| `PauseVmAsync` | `Suspend-VM` | |
| `ResumeVmAsync` | `Resume-VM` | |
| `SaveStateAsync` | `Save-VM` | |
| `CreateSnapshotAsync` | `Checkpoint-VM` | |
| `ApplySnapshotAsync` | `Restore-VMSnapshot -Confirm:$false` | |
| `DeleteSnapshotAsync` | `Remove-VMSnapshot -Confirm:$false` | |
| `GetMeteringAsync` | `Measure-VM` | liefert nur Daten, wenn vorher `Enable-VMResourceMetering` auf der VM aktiviert wurde — das macht das Portal **nicht** selbst |
| `ResizeRamAsync` | `Set-VM -MemoryStartupBytes` | greift real nur bei ausgeschalteter VM bzw. Dynamic-Memory-Konstellation — Hyper-V-Einschränkung, nicht abgefangen |
| `ResizeCpuAsync` | `Set-VM -ProcessorCount` | dito: VM muss aus sein |
| `AttachNetworkAdapterAsync` | `Add-VMNetworkAdapter -SwitchName` | |
| `ResizeVhdAsync` | `Resize-VHD` | nimmt immer die **erste** Platte der VM (`GetFirstVhdPathAsync`) |
| `CompactVhdAsync` | `Optimize-VHD` | dito, erste Platte |
| `ExportVmAsync` | `Export-VM -Path` | Pfad kommt roh vom Client — keine Pfad-Validierung |
| `ImportVmAsync` | `Import-VM -Path` | dito; kurios: Route ist `POST /api/vm/{id}/import`, das `{id}` dient nur der Berechtigungsprüfung, importiert wird aus `importPath` |

### 5.2 Bewusst NICHT implementiert (werfen `NotImplementedException` → HTTP 501)

- **`GetConsoleConnectionAsync` (ConsoleConnect):** Es gibt kein Hyper-V-Cmdlet für
  Konsolenzugriff. `vmconnect.exe` ist eine GUI-Anwendung und liefert keinen Stream,
  den ein Web-Portal durchreichen könnte; eine Web-Konsole bräuchte zusätzliche
  Infrastruktur (RDP-/WebSocket-Gateway wie Guacamole) — außerhalb des Scopes.
- **`CloneVmAsync` (Clone):** Hyper-V hat kein 1:1-Klon-Cmdlet; ein Klon wäre eine
  Export-/Import-Kombination mit Kopiersemantik, Namenskonflikt- und
  Fehlerbehandlung — bewusst nicht umgesetzt.
- **`LiveMigrateVmAsync` (LiveMigrate):** `Move-VM` setzt einen zweiten Hyper-V-Host
  bzw. Cluster mit Live-Migration-Konfiguration voraus; die Testumgebung ist ein
  Einzelhost.

Die Begründungen stehen als Kommentar direkt an den Methoden und landen über die
`NotImplementedException`-Message auch in der 501-Antwort — der API-Nutzer sieht
also *warum* etwas nicht geht. Die Endpunkte und die RBAC-Zuordnung existieren
trotzdem schon (FullAdmin-Aktionen), damit der Aktionskatalog vollständig ist.

### 5.3 Weitere Platzhalter / Lücken zwischen den Schichten

- **`DummyVirtualizationProvider` / `DummyAuthService`:** reine Entwicklungs-Attrappen
  (In-Memory-VM-Liste mit 2 VMs, `Console.WriteLine` statt Aktion, feste Testnutzer).
  Absichtlich so — sie machen die API auf Linux ohne AD/Hyper-V vollständig bedienbar.
- **Das Frontend hinkt der API hinterher:** Es kennt nur Login/Logout, VM-Liste,
  Start, Stop, Reset und Snapshot-Erstellen (`vmApi.js`). Die ganzen neuen Endpunkte
  (Pause, Resume, SaveState, Metering, Snapshot anwenden/löschen, Resize, VHD,
  Export/Import, …) haben **keine UI**. Außerdem ist die UI nicht rollenbewusst:
  Buttons werden jedem angezeigt; ein Viewer bekäme beim Klick schlicht `403`.
- **`VmDetail` lädt über die Liste:** Die Detailseite ruft `getVms()` auf und filtert
  clientseitig nach `id`, statt den existierenden Endpunkt `GET /api/vm/{id}` zu nutzen.
- **`AssignedUserId` ist tot:** `HyperVProvider` mappt das Hyper-V-Notizfeld
  (`Notes = "mugur"`) noch nach `AssignedUserId`, aber seit der Umstellung auf das
  `vmroles`-Claim-Modell wird das Feld **nirgendwo mehr für Autorisierung benutzt**
  — es fährt nur noch als Datenballast mit (und war das M3/M4-Modell "eine VM gehört
  einem Benutzer", das vom Rollenmodell abgelöst wurde).
- **Keine Snapshot-Liste:** Man kann Snapshots erstellen/anwenden/löschen, aber es
  gibt keinen Endpunkt, der die vorhandenen Snapshots einer VM auflistet — anwenden/
  löschen erfordert also, den Namen zu kennen.
- **Keine automatisierten Tests** im Repo (kein Testprojekt in der Solution).
- **SQLite-Autorisierungsschicht ist seit Commit `7df0aae` angeschlossen:** `VmController`
  fragt für die tatsächliche VM-Autorisierung ausschließlich `DbAuthorizationService` ab
  (Kapitel 4.4); der `vmroles`-Claim wird zwar weiterhin erzeugt, aber nirgends mehr
  konsumiert. Konsequenz: Die Testumgebung braucht vollständig befüllte
  `GroupPermissions`, um dieselben Zugriffe wie vorher über `vmroles` zu erhalten — eine
  leere/unvollständige Zuordnungstabelle reicht seit der Umstellung nicht mehr aus (siehe
  Punkt 8 in Kapitel 7).

---

## 6. Konfiguration erklärt

Grundmechanik: ASP.NET Core lädt `appsettings.json` und **überlagert** sie mit
`appsettings.{ASPNETCORE_ENVIRONMENT}.json`. Mit
`ASPNETCORE_ENVIRONMENT=Development` gewinnt also `appsettings.Development.json`
bei allen Schlüsseln, die dort gesetzt sind; alles andere (z. B. `Jwt`) kommt
weiter aus der Basisdatei. Deshalb: `Development` = Dummy-Welt (infrastrukturunabhängig),
`Production` (Default) = LDAP + Hyper-V (Windows Server).

### `appsettings.json` (Basis = Produktion)

- **`Logging`** — Standard-Loglevel (`Information`, ASP.NET-eigene Logs erst ab
  `Warning`). `Default` auf `Debug` stellen aktiviert u. a. das Debug-Log in
  `GetVms`, das die rohen `vmroles`-Claims und die Provider-VM-Namen ausgibt —
  nützlich bei "warum sehe ich meine VM nicht?".
- **`AllowedHosts: "*"`** — welche Host-Header akzeptiert werden; `*` = alle.
- **`Ldap`** → gebunden an `LdapSettings` (`VmPortal.Core/Configuration/LdapSettings.cs`):
  - `Host` (`192.168.122.196`): der Domänencontroller. Falscher Wert → Login schlägt
    mit Verbindungsfehler fehl; alles nach dem Login funktioniert weiter (JWT braucht
    kein AD).
  - `Port` (`389`): unverschlüsseltes LDAP. **Achtung:** einfach 636 eintragen macht
    noch kein LDAPS — der Code setzt `SecureSocketLayer = false` fest (Kapitel 7).
  - `BaseDn` (`DC=testumgebung,DC=local`): Suchbasis für die Gruppenabfrage **und**
    Quelle für den UPN-Realm beim Bind (`benutzer@testumgebung.local`). Falsch →
    Bind oder Gruppensuche scheitert.
  - Fehlt der ganze Abschnitt, verweigert die App den Start
    (`InvalidOperationException` in Program.cs:14) — bewusstes Fail-fast.
- **`Virtualization:Provider`** (`"HyperV"` | alles andere → Dummy): wählt in
  `RegisterVirtualizationProvider` (Program.cs:108) die
  `IVirtualizationProvider`-Implementierung. `HyperV` auf einem Nicht-Windows-System
  → jede VM-Operation endet planmäßig im `502`.
- **`Auth:Provider`** (`"Ldap"` = Default | `"Dummy"`): wählt analog den
  `IAuthService`. `Dummy` akzeptiert nur `mugur`/`jburath` mit `Test1234!`.
- **`Cors:AllowedOrigins`** (`["http://localhost:5173"]`): Origins, die mit
  Credentials (Cookie!) auf die API dürfen — nötig, damit der Vite-Dev-Server
  direkt gegen die API arbeiten kann. In Produktion irrelevant, weil Frontend und
  API dieselbe Origin haben (Frontend liegt in `wwwroot`).
- **`Jwt`** → gebunden an `JwtSettings`:
  - `Secret`: der HMAC-Schlüssel. Wer ihn kennt, kann sich beliebige gültige Tokens
    (inkl. beliebiger `vmroles`) ausstellen — das sensibelste Datum im Projekt, und
    es liegt derzeit im Klartext im Repo (Kapitel 7). Ändern invalidiert sofort alle
    ausgegebenen Tokens (alle Nutzer sind ausgeloggt) — das ist zugleich der einzige
    existierende "Notfall-Revocation-Hebel".
  - `Issuer` / `Audience` (`VmPortal.Api` / `VmPortal.Client`): werden bei der
    Validierung geprüft; ändern invalidiert ebenfalls alle alten Tokens.
  - `ExpiryHours` (8): Gültigkeit des JWT. **Aber:** das Cookie-Ablaufdatum ist im
    `AuthController` separat auf 8 h hart codiert — wer hier z. B. 1 einträgt,
    bekommt ein totes Cookie, das noch 7 h mitgeschickt wird (→ `401`), umgekehrt
    verschwindet bei 24 das Cookie nach 8 h obwohl der Token noch gültig wäre.
- **`ConnectionStrings:VmPortalDb`** (`Data Source=vmportal.db`): SQLite-Dateipfad der
  neuen Autorisierungsschicht (Kapitel 4.4). `RegisterDatabase` in `Program.cs` legt das
  Zielverzeichnis beim Start automatisch an, falls es fehlt — ersetzt aber **nicht** das
  Ausführen der Migration selbst (siehe `docs/authorization.md`).
- **`Authorization:BootstrapFullAdminGroup`** (`VM-Portal-Benutzer`): AD-Gruppe, deren
  Mitglieder ohne DB-Eintrag als FullAdmin gelten (Bootstrap, Kapitel 4.4). Fehlt der
  Abschnitt, verweigert die App den Start (`InvalidOperationException`, analog zu `Ldap`).

### `appsettings.Development.json` (Überlagerung für Entwicklung)

- `Virtualization:Provider = "Dummy"`, `Auth:Provider = "Dummy"` — komplette
  Offline-Welt, ohne AD- und Hyper-V-Anbindung.
- **`TestVmRoles`** → gebunden an `TestVmRolesSettings`: simulierte
  Nutzer→VM→Rolle-Zuordnungen für den `DummyAuthService`, ersetzt die AD-Gruppen:
  `mugur` ist `PowerUser` auf `VM-Mikail`, `jburath` ist `Operator` auf `VM-Burath`.
  Hier kann man beliebige Rollenkonstellationen durchspielen (z. B. `mugur` auf
  `Viewer` setzen und prüfen, dass Start `403` liefert), ohne im AD Gruppen
  anzulegen. Fehlt der Abschnitt, hat schlicht niemand VM-Rollen (leere Liste,
  kein Fehler). In der Basis-`appsettings.json` fehlt er absichtlich — im
  LDAP-Modus wird er ignoriert.
- **`TestAdGroups`** → gebunden an `TestAdGroupsSettings` (Pendant zu `TestVmRoles`, aber
  für den neuen `adgroups`-Claim aus Kapitel 4.4): simulierte AD-Gruppenmitgliedschaften
  pro Testnutzer, z. B. `mugur` → `["VM-Portal-Benutzer"]`, damit er lokal die
  Bootstrap-FullAdmin-Admin-API testen kann, ohne echtes AD.
- `Ldap` und `Jwt` sind hier **nicht** überschrieben → kommen aus der Basisdatei
  (das JWT-Secret ist in Dev und Prod dasselbe). `ConnectionStrings:VmPortalDb` ebenfalls
  nicht — Dev und die lokale Testumgebung nutzen dieselbe `vmportal.db`.

`appsettings.Production.json` überschreibt zusätzlich `ConnectionStrings:VmPortalDb` auf
`C:\VmPortal\data\vmportal.db` und `Authorization:BootstrapFullAdminGroup` auf
`ESX Admins` (Siemens-AD `archiv.mhm.siemens.com`) — siehe Kapitel 4.4 bzw.
`docs/authorization.md`.

Daneben existiert `publish/` mit einem älteren veröffentlichten Build samt eigener
`appsettings.json` — das ist Deploy-Artefakt, nicht Quelle; maßgeblich ist immer
`VmPortal.Api/appsettings*.json`.

---

## 7. Bekannte Baustellen und offene Punkte (ehrlich)

**Autorisierung / AD:**

1. **AGDLP ist nicht umgesetzt.** Aktuell werden die Rollengruppen
   (`VM-Mikail-PowerUser`) direkt aus `memberOf` des Benutzers gelesen. Das
   AD-Best-Practice-Modell AGDLP (Account → Globale Gruppe → Domänenlokale Gruppe →
   Permission) würde bedeuten: Nutzer sind in globalen Gruppen, die wiederum in den
   domänenlokalen Rollengruppen stecken. Technisch relevant: `memberOf` liefert nur
   **direkte** Mitgliedschaften — bei verschachtelten Gruppen würde das Portal die
   Rolle **nicht sehen**. Eine Umsetzung bräuchte rekursive Auflösung bzw. den
   AD-Spezialfilter `memberOf:1.2.840.113556.1.4.1941:=` (LDAP_MATCHING_RULE_IN_CHAIN).
2. **Mehrdeutigkeit im Gruppennamen-Parsing.** Das Muster `VM-{VmName}-{Rolle}`
   trennt am Bindestrich — VM-Namen dürfen aber selbst Bindestriche enthalten.
   Endet ein VM-Name auf ein Rollenwort (z. B. VM `VM-Kunde-Operator`), ist der
   Gruppenname `VM-Kunde-Operator` nicht mehr eindeutig: Der Regex interpretiert ihn
   als "Rolle `Operator` auf `VM-Kunde`" — gemeint sein könnte aber etwas ganz
   anderes. Die Konvention funktioniert nur, solange kein VM-Name auf
   `-Viewer|-Operator|-PowerUser|-Admin|-FullAdmin` endet; erzwungen wird das nirgends.
3. **Keine Token-Revocation.** Wird einem Nutzer im AD eine Rollengruppe entzogen,
   bleibt sein bereits ausgestelltes JWT bis zu 8 Stunden mit den **alten** Rollen
   gültig — die VM-Rollen werden nur beim Login gelesen. Auch Logout löscht nur das
   Cookie im Browser; der Token selbst bleibt bis zum Ablauf technisch gültig (wer
   ihn z. B. aus einem Netzwerk-Trace hat, kann ihn weiterverwenden). Abhilfe wäre
   eine Token-Blacklist oder kurzlebige Tokens + Refresh — beides fehlt.

**Transport / Secrets:**

4. **LDAP läuft im Klartext, und LDAPS ist entgegen der Thesis-Formulierung NICHT
   rein per Konfiguration aktivierbar.** `bachelorarbeit.tex` behauptet, die
   Umstellung auf LDAPS sei "im Code vorbereitet, erforderlich ist allein die
   Konfiguration (Host, Port, TLS-Flag)". Tatsächlich hat `LdapSettings` **kein**
   TLS-Flag, und `LdapAuthService` setzt `SecureSocketLayer = false` fest — Port 636
   in der Config allein bewirkt nichts. Das ist eine echte Diskrepanz zwischen
   Thesis-Text und Code; entweder Code nachziehen (Flag + `SecureSocketLayer`
   danach setzen) oder Thesis-Formulierung korrigieren. Beim einfachen Bind geht das
   Passwort damit derzeit unverschlüsselt über die (Test-)Leitung.
5. **JWT-Secret und Verbindungsdaten liegen im Klartext im Repo**
   (`appsettings.json`, zusätzlich nochmal im eingecheckten... genauer: im
   Arbeitsverzeichnis liegenden `publish/`-Ordner). Für die Testumgebung akzeptiert
   und im README so ausgewiesen; Auslagerung in Umgebungsvariablen/Secret-Store ist
   als Phase 6 eingeplant, aber offen.
6. **`Secure`-Cookie vs. HTTP-Testbetrieb:** Das Cookie wird mit `Secure` gesetzt,
   die Testumgebung läuft aber über `http://192.168.122.196:5000`. Browser
   akzeptieren Secure-Cookies über HTTP nur auf `localhost` — direkter
   Browser-Zugriff über die IP würde das Login-Cookie verlieren. Der Vite-Dev-Proxy
   kaschiert das (Browser redet mit `localhost:5173`); für ein Produktiv-Deployment
   braucht es HTTPS (Reverse Proxy), das die Thesis auch als offen ausweist.
7. **Cookie-Lebensdauer 8 h hart codiert** im `AuthController`, während der Token
   `Jwt:ExpiryHours` nutzt — beide Werte können auseinanderlaufen (Details Kapitel 6).

**Fehlende Funktionalität:**

8. **M6 (Rest) offen, M8 (Evaluation) offen — M7 seit 2026-08-19 erledigt:** Die
   SQLite-Autorisierungsschicht (Rollen, VM-Gruppen, AD-Gruppen-Zuordnungen, Admin-API)
   existiert seit 2026-08-12 (Kapitel 4.4) und ist seit Commit `7df0aae` auch in
   `VmController` verdrahtet — die tatsächliche VM-Autorisierung läuft jetzt
   ausschließlich über `DbAuthorizationService` (`adgroups`-Claim), nicht mehr über
   `vmroles`. **Kapitel 3, 4.2, 4.3 und 4.4 dieses Dokuments beschreiben an mehreren
   Stellen noch den `vmroles`-Pfad als aktiven Autorisierungsweg — das ist seit der
   Umstellung nicht mehr korrekt und hier bewusst nicht mehr flächendeckend
   nachgezogen worden** (separate, größere Überarbeitung nötig). M7 (WinRM-Multi-Host,
   Login gegen Produktionsdomäne, DB-first-Autorisierung statt N+1-Full-Inventory-Scan)
   ist seit 2026-08-19 erledigt, siehe Update-Hinweis am Dateianfang sowie Abschnitt 2/5.1.
   Nach wie vor offen: kein **Audit-Log** (wer hat wann welche VM-Aktion ausgeführt? — steht
   nur flüchtig im Konsolen-Log), M6 (Rest, siehe CLAUDE.md) und Evaluation (M8) ausstehend.
9. **Frontend deckt nur einen Bruchteil der API ab** und ist nicht rollenbewusst
   (Kapitel 5.3); keine Snapshot-Liste; `VmDetail` nutzt `GET /api/vm` statt
   `GET /api/vm/{id}`.
10. **Kein Testprojekt** in der Solution — die "testbare API" aus M2 meint manuelle
    Testbarkeit via Dummy-Provider, nicht automatisierte Tests.
11. **Export/Import ohne Pfad-Validierung:** `exportPath`/`importPath` kommen roh
    vom Client zum `Export-VM`/`Import-VM`-Cmdlet. Zwar nur für FullAdmins erreichbar
    und dank `AddParameter` keine Command-Injection, aber ein FullAdmin einer
    einzelnen VM kann damit auf beliebige Host-Pfade schreiben/lesen.

**Doku-Diskrepanzen (README / CLAUDE.md vs. Code) — Stand 2026-08-12:**

12. ✅ **Behoben:** Die README-Endpunkttabelle war veraltet (nur 6 von ~20
    `VmController`-Endpunkten). Ist jetzt vollständig (inkl. `501`-Endpunkte) und um die
    vier neuen Admin-Controller ergänzt.
13. ✅ **Behoben:** Die README beschrieb noch das alte "VM gehört einem Benutzer"-Modell
    (`AssignedUserId`/Notizfeld), später den inzwischen abgelösten `vmroles`-Claim-Pfad als
    aktiven Autorisierungsweg. Beschreibt jetzt den tatsächlichen Weg über
    `DbAuthorizationService`/`adgroups`-Claim (Stand seit Commit `7df0aae`) und erwähnt
    `vmroles` nur noch als weiterhin erzeugten, aber unkonsumierten Claim; JWT-Claims
    (`username`, `role`, `vmroles`, `adgroups`) sind vollständig genannt.
14. ✅ **Gegenstandslos:** Ein separates `thesis/CLAUDE.md` existiert nicht (mehr) — es
    gibt nur noch das eine `CLAUDE.md` im Repo-Root, das mit dem Autorisierungs-Update
    aktuell gehalten wurde.
15. ✅ **Behoben:** Das Root-`CLAUDE.md` ist wieder eingecheckt. `publish/` (Build-Output
    mit Secrets) ist weiterhin untracked und gehört langfristig in `.gitignore`, statt
    nur uncommitted im Arbeitsverzeichnis zu liegen — das ist der einzige Rest dieses
    Punktes, der noch offen ist.
16. ✅ **Behoben (2026-08-19):** CLAUDE.md, README.md und diese Datei beschrieben den
    WinRM-Remote-Modus noch als "nicht implementiert, nur Konnektivität verifiziert"
    bzw. führten noch die ursprünglich vier statt drei Hyper-V-Hosts. Alle drei Dateien
    aktualisiert auf den tatsächlichen Stand: Remote-Modus implementiert und produktiv
    gegen alle drei Hosts getestet, Login gegen die Produktionsdomäne funktioniert,
    DB-first-Autorisierung statt N+1-Full-Inventory-Scan. Zusätzlich wurde in CLAUDE.md
    eine Dokumentationspflicht ergänzt (proaktive Aktualisierung dieser drei Dateien
    nach jeder funktional relevanten Aufgabe), damit solche Diskrepanzen künftig seltener
    entstehen.

---

## 8. Glossar

- **RBAC (Role-Based Access Control):** Zugriffsrechte hängen an Rollen, nicht an
  Einzelpersonen. Statt "mugur darf VM-Mikail starten" sagt man "Operatoren dürfen
  starten, mugur ist Operator von VM-Mikail".
- **JWT (JSON Web Token):** Ein signiertes "Ticket" in Textform, das Angaben über
  den Nutzer enthält (Name, Rollen, Ablaufzeit). Der Server erkennt an der Signatur,
  dass er es selbst ausgestellt hat und niemand daran herumgeschrieben hat.
- **Claim:** Eine einzelne Aussage in so einem Token, z. B. "name = mugur" oder
  "vmroles = [VM-Mikail: PowerUser]".
- **HMAC-SHA256:** Das Signaturverfahren des JWT: aus Token-Inhalt + geheimem
  Schlüssel wird eine Prüfsumme gebildet. Nur wer das Secret kennt, kann gültige
  Signaturen erzeugen.
- **LDAP (Lightweight Directory Access Protocol):** Das Standardprotokoll, um mit
  einem Verzeichnisdienst (z. B. Active Directory) zu reden — Benutzer suchen,
  Attribute lesen, sich anmelden. Standardport 389.
- **LDAPS:** LDAP über TLS verschlüsselt (Port 636), damit Passwörter beim Bind
  nicht im Klartext übers Netz gehen.
- **Bind:** Die LDAP-Anmeldeoperation. "Ein Bind gelingt" heißt: Benutzername +
  Passwort waren korrekt — das nutzt das Portal als Passwortprüfung.
- **AD (Active Directory):** Microsofts Verzeichnisdienst: die zentrale Datenbank
  eines Windows-Netzwerks für Benutzer, Gruppen und Computer.
- **DN (Distinguished Name):** Die "vollständige Adresse" eines Objekts im
  Verzeichnis, z. B. `CN=VM-Mikail-PowerUser,OU=Gruppen,DC=testumgebung,DC=local`.
- **CN (Common Name):** Der eigentliche Name eines Objekts — das erste Stück des DN
  (hier: `VM-Mikail-PowerUser`).
- **UPN (User Principal Name):** Anmeldename im E-Mail-Format,
  `mugur@testumgebung.local` — so meldet sich das Portal beim Bind an.
- **sAMAccountName:** Der klassische kurze Windows-Anmeldename (`mugur`); danach
  sucht das Portal den Benutzereintrag im AD.
- **memberOf:** AD-Attribut eines Benutzers, das die Gruppen auflistet, in denen er
  **direkt** Mitglied ist — die Quelle der VM-Rollen.
- **AGDLP:** Microsofts Empfehlung zum Gruppen-Schachteln: **A**ccounts in
  **G**lobale Gruppen, die in **D**omänen-**L**okale Gruppen, und nur letztere
  bekommen **P**ermissions. Macht Rechteverwaltung skalierbar; hier noch nicht
  umgesetzt.
- **OID (Object Identifier):** Weltweit eindeutige Nummernfolge zur Kennzeichnung
  von Standards/Regeln, z. B. `1.2.840.113556.1.4.1941` = die AD-Spezialregel, die
  bei einer LDAP-Suche verschachtelte Gruppen mit auflöst.
- **RSAT (Remote Server Administration Tools):** Microsofts Admin-Werkzeugkasten
  (u. a. "Active Directory-Benutzer und -Computer"), mit dem man auf dem
  Testserver Benutzer und Gruppen pflegt.
- **httpOnly (Cookie-Flag):** Das Cookie ist für JavaScript unsichtbar — nur der
  Browser selbst schickt es mit. Schutz gegen Token-Diebstahl per XSS.
- **SameSite=Strict (Cookie-Flag):** Der Browser schickt das Cookie nur mit, wenn
  die Anfrage von der eigenen Seite ausgeht. Schutz gegen CSRF.
- **XSS (Cross-Site Scripting):** Angriff, bei dem fremder JavaScript-Code in die
  Seite eingeschleust wird und dann z. B. Tokens aus `localStorage` klauen könnte.
- **CSRF (Cross-Site Request Forgery):** Angriff, bei dem eine fremde Webseite den
  Browser dazu bringt, mit den vorhandenen Cookies ungewollte Anfragen an die API
  zu schicken.
- **CORS (Cross-Origin Resource Sharing):** Browser-Regelwerk, das festlegt, welche
  fremden Origins (Protokoll+Host+Port) eine API aufrufen dürfen — hier nur für den
  Vite-Dev-Server freigeschaltet.
- **Origin:** Kombination aus Protokoll, Host und Port (`http://localhost:5173`).
  Gleiche Origin = keine CORS-Hürden.
- **SPA (Single-Page-Application):** Webanwendung aus einer einzigen HTML-Seite;
  Seitenwechsel macht JavaScript (React Router), Daten kommen per API.
- **Middleware:** Ein Glied in der Verarbeitungskette einer ASP.NET-Anfrage; jede
  Anfrage läuft der Reihe nach durch alle (Fehlerbehandlung → Auth → Controller).
- **DI (Dependency Injection):** Klassen bekommen ihre Abhängigkeiten (als
  Interfaces) von außen gereicht, statt sie selbst zu erzeugen — dadurch sind
  Implementierungen austauschbar (Dummy ↔ HyperV).
- **Cmdlet:** Ein PowerShell-Befehl im Verb-Substantiv-Schema (`Start-VM`,
  `Get-VM`) — die offizielle Automatisierungsschnittstelle von Hyper-V.
- **Runspace:** Eine PowerShell-Ausführungsumgebung innerhalb des .NET-Prozesses —
  darin führt der `HyperVProvider` die Cmdlets aus, ohne externen Prozess.
- **WinRM (Windows Remote Management):** Windows-Fernwartungsprotokoll für
  PowerShell-Remoting. Wurde hier zunächst benutzt, dann verworfen, weil die App
  direkt auf dem Host läuft.
- **Hypervisor:** Die Software, die VMs betreibt (Hyper-V, Proxmox, KVM, ESXi).
- **VHD/VHDX:** Dateiformat der virtuellen Festplatten von Hyper-V.
- **Snapshot/Checkpoint:** Eingefrorener Zustand einer VM, auf den man
  zurückspringen kann. Hyper-V nennt es Checkpoint, das Cmdlet heißt `Checkpoint-VM`.
- **Live-Migration:** Umzug einer laufenden VM auf einen anderen Host ohne
  Ausfallzeit — braucht mindestens zwei Hosts, daher hier nicht umsetzbar.
- **Resource Metering:** Hyper-V-Funktion, die den Ressourcenverbrauch (CPU, RAM,
  Disk) einer VM aufzeichnet; muss pro VM per `Enable-VMResourceMetering`
  eingeschaltet werden, ausgelesen wird mit `Measure-VM`.
- **REST (Representational State Transfer):** API-Stil über HTTP: Ressourcen als
  URLs (`/api/vm/VM-Mikail`), Aktionen als HTTP-Methoden (GET lesen, POST
  ausführen), Zustand in Statuscodes (200/401/403/404/501/502).
- **Kestrel:** Der in ASP.NET Core eingebaute Webserver, der die App ausliefert.
- **JTI (JWT ID):** Claim mit einer Zufalls-ID pro Token — macht jeden Token
  eindeutig identifizierbar (wäre die Grundlage für eine Token-Blacklist).
