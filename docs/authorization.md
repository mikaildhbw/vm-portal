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

Der bisherige `vmroles`-Claim (VM-Name → Rolle, geparst aus AD-Gruppen nach dem Schema
`VM-{VmName}-{Rolle}`) bleibt unverändert bestehen und wird weiterhin von `VmController`
genutzt — die neue Schicht ergänzt, ersetzt aber (noch) nicht die bestehende
VM-Autorisierung in der API. `TestVmRolesSettings`/`DummyAuthService` simulieren
weiterhin lokal VM-Rollen für diesen alten Pfad; für den neuen DB-Pfad simuliert
`TestAdGroupsSettings` (Abschnitt `TestAdGroups` in `appsettings.Development.json`)
AD-Gruppenmitgliedschaften.

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
