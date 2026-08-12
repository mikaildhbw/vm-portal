# Autorisierungsschicht (SQLite/EF Core)

## Architekturprinzip: Hybrid aus AD-Authentifizierung und DB-Autorisierung

VmPortal trennt zwei Belange, die vorher beide implizit über AD-Gruppennamen liefen:

- **Authentifizierung** ("Wer ist der Nutzer?") bleibt unverändert Aufgabe von
  `LdapAuthService`: Bind gegen das AD, Auslesen der `memberOf`-Gruppenmitgliedschaften.
- **Autorisierung** ("Was darf dieser Nutzer?") übernimmt neu `DbAuthorizationService`
  gegen eine lokale SQLite-Datenbank (`vmportal.db`). Die AD-Gruppen des Nutzers werden
  dazu roh (als Gruppennamen, ohne Rollen-Suffix-Parsing) über einen neuen JWT-Claim
  `adgroups` transportiert (`VmPortal.Core.Services.AdGroupClaims`) und gegen die Tabelle
  `UserGroups` gematcht.

**Update 2026-08-12:** `VmController` nutzt seit dieser Umstellung ausschließlich
`DbAuthorizationService` (Konstruktor-Injection von `IDbAuthorizationService`) für die
VM-Autorisierungsentscheidung. Die alte, direkt im Controller ausgeführte Prüfung
`vmRoles.TryGetValue(...) && RolePermissions.IsAllowed(role, action)` wurde ersatzlos aus
`VmController` entfernt (nicht nur auskommentiert), ebenso die private Hilfsmethode
`GetVmRolesFromToken()`.

Der `vmroles`-Claim selbst (VM-Name → Rolle, geparst aus AD-Gruppen nach dem Schema
`VM-{VmName}-{Rolle}`) wird **weiterhin unverändert erzeugt** — `JwtTokenService`,
`LdapAuthService`, `VmRoleClaims` und `RolePermissions` sind von dieser Umstellung nicht
betroffen. Er hat aktuell keinen bekannten Konsumenten mehr (das Frontend liest ihn nicht;
`VmController` war der einzige), bleibt aber im JWT erhalten für einen möglichen künftigen
Zweck wie eine reine Anzeige "meine höchste VM-Rolle" im Frontend, ohne dafür erneut das
Token-Schema ändern zu müssen. `TestVmRolesSettings`/`DummyAuthService` erzeugen ihn
lokal unverändert weiter (Abschnitt `TestVmRoles` in `appsettings.Development.json`), auch
wenn er zur Laufzeit nicht mehr ausgewertet wird.

Für die tatsächliche Autorisierung zählt jetzt ausschließlich der `adgroups`-Claim
(rohe AD-Gruppennamen) gegen die Tabelle `UserGroups`; lokal simuliert über
`TestAdGroupsSettings` (Abschnitt `TestAdGroups` in `appsettings.Development.json`).

## Schema

```
VirtualServers          Hyper-V-Hosts (Address, Platform, Name)
VirtualMachines          Autorisierungs-Metadaten je VM (ServerId, GroupId nullable, Name)
VirtualMachineGroups      Gruppierung von VMs, Ziel der Rechtevergabe
UserGroups                1:1-Abbild einer AD-Gruppe (nur Name, kein Sync)
Roles                     5 System-Rollen + beliebig viele Custom-Rollen
VMActions                 Katalog aller 22 autorisierbaren VM-Aktionen (VmAction-Enum)
RoleActions                Rolle x Aktion (explizite, vollständige Rechte-Menge einer Rolle)
GroupPermissions           UserGroup x VmGroup x Role (wer darf was auf welcher VM-Gruppe)
```

`VirtualMachines.GroupId` ist nullable: Eine VM ohne Gruppe ist bewusst unsichtbar und
nicht zugreifbar (secure-by-default). Das ist kein Fehlerzustand, sondern der
Ausgangszustand jeder neu erfassten VM, bis ein Admin sie einer Gruppe zuordnet.

### RBAC statt Level-Hierarchie

Frühere Entwürfe sahen eine strikte Level-Hierarchie vor (höhere Rolle erbt automatisch
alle Rechte niedrigerer Rollen — so, wie es das bestehende `RolePermissions`/`VmRole`-Enum
für die alte `vmroles`-Autorisierung noch tut). Die neue Schicht ersetzt das durch
klassisches RBAC: Jede Rolle definiert sich über ihre `RoleActions` — eine explizite,
vollständige Liste erlaubter Aktionen, ohne implizite Vererbung. `Roles.Level` existiert
weiterhin, dient aber nur noch der Sortierung/Anzeige in der Admin-UI.

**Warum die Abkehr von der Hierarchie?** Custom-Rollen sind frei zusammenstellbar (z. B.
eine Rolle "Snapshot-Operator" mit genau `SnapshotCreate` + `SnapshotApply`, ohne
`Start`/`Stop`). Solche Rollen lassen sich nicht mehr sinnvoll in eine totale Ordnung
("höher"/"niedriger") einsortieren — es gibt keine eindeutige Antwort auf "ist
Snapshot-Operator höher oder niedriger als Operator?". RBAC mit expliziten Aktionsmengen
kommt ohne diese Annahme aus.

Die fünf System-Rollen (`Viewer`, `Operator`, `PowerUser`, `Admin`, `FullAdmin`,
`IsSystemRole = true`) sind nicht löschbar, nicht umbenennbar, und ihre `RoleActions`
sind nicht editierbar (siehe `RolesController`). Ihre initialen `RoleActions` werden beim
Seed **aus der bestehenden `RolePermissions`-Klasse** übernommen (`AuthorizationSeedData`
ruft `RolePermissions.IsAllowed(role, action)` für jede Kombination auf) — der bisherige
Berechtigungsstand wird also 1:1 in explizite Aktionsmengen überführt, nicht neu erfunden.

## Bootstrap-Mechanismus

Damit nach einem frischen Deployment (leere `GroupPermissions`-Tabelle) überhaupt jemand
Rollen und Zuordnungen über die Admin-UI anlegen kann, gibt es einen Bootstrap-Pfad:
Ist eine der AD-Gruppen des Nutzers gleich der konfigurierten Gruppe
`Authorization:BootstrapFullAdminGroup`, gilt er ohne weitere Prüfung als FullAdmin mit
allen 22 Aktionen auf jeder VM. Der Wert ist umgebungsabhängig:

| Umgebung | Datei | AD | Bootstrap-Gruppe |
| --- | --- | --- | --- |
| Lokale Testumgebung | `appsettings.json` | `testumgebung.local` | `VM-Portal-Benutzer` |
| Produktion | `appsettings.Production.json` | `archiv.mhm.siemens.com` | `ESX Admins` |

Beide Gruppennamen werden zusätzlich als `UserGroups`-Zeilen geseedet, damit sie auch
als ganz normales Ziel einer `GroupPermission`-Zuordnung referenzierbar sind, falls ein
Admin ihnen später zusätzlich (nicht-globale) Rollen auf einzelnen VM-Gruppen zuweisen
möchte.

Die Admin-Endpunkte (`/api/admin/*`) verlangen ausschließlich diesen globalen
Bootstrap-FullAdmin-Status (`AdminControllerBase`) — Rollenverwaltung ist eine globale,
nicht VM-Gruppen-gebundene Angelegenheit, eine feingranularere Prüfung über
`GroupPermissions` wäre hier nicht sinnvoll anwendbar.

## Autorisierungslogik: Union statt "höchste Rolle gewinnt"

Für einen Nutzer (gegeben durch seine AD-Gruppen aus dem `adgroups`-Claim) und eine VM
ermittelt `DbAuthorizationService.GetAllowedActionsAsync`:

1. **Bootstrap-Check** (s. o.) — falls zutreffend, sofort alle 22 Aktionen.
2. VM → `VirtualMachineGroup` auflösen. Keine Gruppe → keine Rechte (leere Menge).
3. Alle `GroupPermissions` finden, deren `VmGroupId` passt **und** deren `UserGroupId`
   einer der AD-Gruppen des Nutzers entspricht. Ein Nutzer kann über mehrere
   AD-Gruppen mehrere Treffer gleichzeitig haben.
4. **Erlaubte Aktionen = Union der `RoleActions` aller zutreffenden Rollen.**
   `Roles.Level` fließt in diese Berechnung nicht ein — er ist rein kosmetisch.

**Warum Union und nicht "höchste Rolle gewinnt"?** "Höchste Rolle gewinnt" setzt voraus,
dass sich alle beteiligten Rollen eindeutig in eine Rangfolge bringen lassen — das gilt
bei den fünf System-Rollen noch (sie sind bewusst so konstruiert), aber nicht mehr bei
frei zusammenstellbaren Custom-Rollen (s. o., Abschnitt "RBAC statt Level-Hierarchie").
Hat ein Nutzer z. B. über zwei AD-Gruppen sowohl die Custom-Rolle "Snapshot-Operator"
(`SnapshotCreate`, `SnapshotApply`) als auch die Custom-Rolle "Netzwerk-Operator"
(`AttachNetworkAdapter`) auf derselben VM-Gruppe, gibt es keine der beiden Rollen, die
"höher" ist und die Rechte der anderen automatisch mit einschlösse — "höchste Rolle
gewinnt" würde hier eine der beiden Aktionsmengen willkürlich verwerfen. Die Union ist
bei frei zusammenstellbaren, nicht notwendigerweise hierarchisch verschachtelten
Aktionsmengen die einzige widerspruchsfreie Kombinationsregel: Sie verliert nie explizit
gewährte Rechte und erfindet nie welche hinzu, unabhängig davon, wie viele Rollen über
wie viele AD-Gruppen zutreffen.

Diese Design-Entscheidung ist der Grund, warum `GroupPermissions` bewusst **nicht** über
`UNIQUE(VmGroupId, UserGroupId)` eindeutig ist, sondern über
`UNIQUE(VmGroupId, UserGroupId, RoleId)`: Ein UserGroup/VmGroup-Paar kann mehrere Rollen
gleichzeitig haben (z. B. eine AD-Gruppe bekommt zwei verschiedene Custom-Rollen auf
dieselbe VM-Gruppe zugewiesen), was die Admin-UI als Mehrfachauswahl statt Einzelauswahl
pro Zuordnung abbilden muss.

## Verweigerungsgründe in den Logs unterscheiden

Da `VmController` jetzt ausschließlich `DbAuthorizationService` befragt, muss ein `403`
nicht mehr zwangsläufig heißen "Nutzer hat wirklich keine Rechte" — es kann in der
Übergangsphase auch heißen "die GroupPermission dafür wurde in der neuen DB noch nicht
angelegt". Damit man das beim Debuggen unterscheiden kann, loggen `DbAuthorizationService`
und `VmController` mit unterschiedlichen, grep-baren Präfixen (jeweils `LogWarning`, außer
der Authentifizierung):

| Fall | Log-Zeile | Wo |
| --- | --- | --- |
| Kein/ungültiges JWT-Cookie | `nicht authentifiziert: {Method} {Path} ({Reason})` | `Program.cs`, `JwtBearerEvents.OnChallenge` |
| VM in der Autorisierungs-DB unbekannt oder ohne `GroupId` | `DB-Autorisierung verweigert (VM ohne Gruppe): …` | `DbAuthorizationService.GetAllowedActionsAsync` |
| Keine der AD-Gruppen des Nutzers ist einer `GroupPermission` auf der betroffenen VM-Gruppe zugeordnet (AD-Gruppe ggf. gar nicht als `UserGroup` bekannt, oder bekannt aber ohne passende Zuordnung) | `DB-Autorisierung verweigert (keine passende GroupPermission): …` | `DbAuthorizationService.GetAllowedActionsAsync` |
| Zuordnung existiert, aber die konkrete Aktion ist nicht in den `RoleActions` der zugewiesenen Rolle(n) enthalten (normale RBAC-Verweigerung, z. B. Operator versucht `SnapshotDelete`) | `403: Nutzer {User} (AD-Gruppen […]) darf Aktion {Action} auf VM {VmName} nicht ausführen` | `VmController.AuthorizeVmActionAsync` (einzige Log-Zeile für diesen Fall — `DbAuthorizationService` loggt hier bewusst nichts, da das erwartetes, alltägliches RBAC-Verhalten ist, kein Konfigurationsproblem) |

Die ersten beiden Fälle sind bewusst als potenzielle Konfigurationslücken markiert (nicht
als Bug): Direkt nach der Umstellung von `VmController` auf `DbAuthorizationService` ist es
erwartbar, dass Nutzer, die vorher über den `vmroles`-Claim Zugriff hatten, jetzt `403`
bekommen, weil für ihre VM(s) noch keine `VirtualMachineGroup`-Zuordnung bzw. keine
passende `GroupPermission` in der neuen DB existiert. Abhilfe ist ausschließlich
Admin-Konfiguration (VM-Gruppen anlegen, VMs zuordnen, `GroupPermissions` über
`/api/admin/*` vergeben) — kein Code-Fix.

## Migration & Deployment

Die Migration `InitialAuthorizationSchema` (`VmPortal.Core/Data/Migrations/`) wurde mit

```
dotnet ef migrations add InitialAuthorizationSchema --project VmPortal.Core --startup-project VmPortal.Api --output-dir Data/Migrations
```

erzeugt und enthält neben dem Schema auch die Seed-Daten aus `AuthorizationSeedData`
(`HasData` in `VmPortalDbContext.OnModelCreating`). Sie wird **nicht** automatisch beim
Start der Anwendung ausgeführt (kein `context.Database.Migrate()` in `Program.cs`).

**TODO Deployment:** `dotnet ef database update --project VmPortal.Core --startup-project
VmPortal.Api` muss als eigener Schritt Teil des Deployment-Prozesses werden, bevor die
Anwendung erstmals mit einer neuen Migration gestartet wird (es existiert noch kein
`deploy.ps1` in diesem Repo, das diesen Schritt aufnehmen könnte — sobald eines angelegt
wird, gehört dieser Befehl dort hinein). Das Zielverzeichnis der SQLite-Datei
(`C:\VmPortal\data` in Produktion) wird beim Start der Anwendung automatisch angelegt
(`RegisterDatabase` in `Program.cs`), falls es fehlt — das ersetzt nicht das Ausführen
der Migration selbst.
