# Deployment auf der Produktions-VM

`deploy.ps1` (Repo-Root) automatisiert wiederholte Deployments auf der Windows-Produktions-VM.
Es wird **auf der VM selbst** manuell per PowerShell ausgeführt (per Guacamole bedient) -
nicht von einem CI-System und nicht von Claude Code aus.

## Voraussetzungen

- **Repo bereits einmalig geklont** unter `C:\VmPortal\vm-portal` (Default von `-RepoPath`),
  inklusive funktionierendem Git-Zugriff (SSH-over-HTTPS-Tunnel `ssh.github.com:443` mit
  Deploy Key `vmportal_deploy` - bereits manuell verifiziert). `deploy.ps1` clont **nicht**
  selbst; ohne vorhandenes Repo bricht es mit einer klaren Fehlermeldung ab.
- **.NET 8 SDK** installiert und im `PATH` (Zielpfad laut Verifikation: `C:\dotnet`).
- **`dotnet-ef`-Tool** global installiert (`dotnet tool install --global dotnet-ef`) - wird
  für den Migrationsschritt gebraucht und ist nicht Teil des SDK.
- **`C:\VmPortal\data`** muss beschreibbar sein (die Anwendung legt das Verzeichnis selbst an,
  falls es fehlt, siehe `docs/authorization.md`; die EF-Migration braucht es aber schon vor dem
  ersten App-Start).
- Kein anderer Prozess darf dauerhaft auf Port `5000` (Default von `-Port`) lauschen.

### Hinweis zu Secrets in `appsettings.Production.json`

Die im Repo eingecheckte `appsettings.Production.json` überschreibt nur `ConnectionStrings`
und `Authorization`; `Jwt:Secret` kommt weiterhin aus der Basis-`appsettings.json` (Testwert,
siehe CLAUDE.md Phase-6-TODO zur Secret-Auslagerung). Falls auf der Produktions-VM ein
echtes, von der Basisdatei abweichendes Secret lokal in `appsettings.Production.json`
eingetragen wird, ist das eine **uncommittete lokale Änderung an einer versionierten Datei**:
`git pull` (Schritt 2 im Skript) schlägt dann bei einem Konflikt bewusst mit einer klaren
Fehlermeldung fehl, statt die lokale Änderung stillschweigend zu überschreiben. Bis die
Secret-Auslagerung (Umgebungsvariable/Secret-Store) umgesetzt ist, muss das nach jedem
fehlgeschlagenen Pull manuell aufgelöst werden.

## Aufruf

```powershell
.\deploy.ps1 -Branch main
```

Mit abweichenden Pfaden/Port (z. B. für einen Testlauf):

```powershell
.\deploy.ps1 -Branch main -RepoPath C:\VmPortal\vm-portal -PublishPath C:\VmPortal\publish -Port 5000
```

Weitere Parameter: `-HealthCheckTimeoutSeconds` (Default 30), `-HealthCheckIntervalSeconds`
(Default 2).

## Ablauf (Kurzfassung)

1. Prüft, ob `-RepoPath` ein Git-Repository ist (kein Auto-Clone).
2. `git fetch` + `git checkout -Branch` + `git pull` - bricht bei Merge-Konflikten mit
   klarer Fehlermeldung ab.
3. Stoppt einen laufenden `VmPortal.Api.dll`-Prozess (Suche über die Kommandozeile, da noch
   kein Windows-Dienst registriert ist - siehe TODO im Skript und Abschnitt "Was es nicht
   tut"). **Bewusst vor** Migration und Publish, nicht danach: Sowohl die SQLite-Datei als
   auch die Publish-DLLs dürfen dabei nicht durch den alten Prozess gesperrt sein. Die vom
   Aufgabenkontext geforderte Reihenfolge "Migration vor dem Neustart" bleibt davon
   unberührt - migriert wird weiterhin, bevor die Anwendung neu gestartet wird, nur eben
   nach dem (ohnehin fälligen) Stoppen des alten Prozesses.
4. `dotnet ef database update` gegen `appsettings.Production.json`
   (`ASPNETCORE_ENVIRONMENT=Production`, damit dieselbe Konfigurationsauflösung wie zur
   Laufzeit greift, ohne den Connection-String im Skript zu duplizieren).
5. Leert `-PublishPath` vollständig und führt `dotnet publish VmPortal.Api -c Release`
   dorthin aus (verhindert Datei-Leichen aus früheren Versionen - `dotnet publish` räumt das
   Zielverzeichnis von sich aus nicht auf).
6. Startet die Anwendung per `Start-Process` mit `--urls http://0.0.0.0:5000` (bzw. `-Port`),
   stdout/stderr umgeleitet in Log-Dateien im Publish-Verzeichnis.
7. Health-Check gegen `http://localhost:<Port>/` mit Timeout/Retry. Es gibt aktuell keinen
   dedizierten Health-Endpoint (TODO: z. B. `/healthz` ergänzen); bis dahin zählt jede
   HTTP-Antwort - auch ein 404 - als "Anwendung läuft und nimmt Verbindungen an", nur eine
   Verbindungsverweigerung/ein Timeout gilt als Fehlschlag.
8. Gibt eine Zusammenfassung aus: Branch, Commit-Hash, Zeitstempel, Health-Check-Ergebnis.

Jeder Schritt bricht bei Fehlschlag sofort mit einer Meldung ab, die den betroffenen Schritt
klar benennt (`FEHLER in Schritt '...'`); es wird nicht stillschweigend weitergemacht.

## Was `deploy.ps1` NICHT tut

- **Kein automatisches Rollback.** Schlägt ein Schritt fehl (z. B. Migration oder
  Health-Check), bleibt die VM in dem Zustand, in dem der fehlgeschlagene Schritt sie
  hinterlassen hat - es wird kein vorheriger Stand wiederhergestellt. Das ist manuell zu
  beheben (z. B. `git checkout <alter-commit>` + Skript erneut ausführen).
- **Kein CI/CD-Trigger.** Das Skript läuft ausschließlich manuell auf der VM; es gibt keine
  GitHub-Actions-Anbindung, keinen Webhook und keinen automatischen Aufruf nach einem Push.
- **Kein Windows-Dienst-Setup.** Die Anwendung läuft als einfacher `dotnet`-Prozess, den das
  Skript per `Start-Process` startet und per Kommandozeilensuche wiederfindet/stoppt - nicht
  als registrierter Windows-Dienst mit automatischem Neustart bei Absturz oder Server-Reboot.
  Ein `New-Service`/`sc.exe create`-Setup ist als TODO im Skript markiert.
- **Kein Frontend-Build.** `VmPortal.Frontend` wird von diesem Skript nicht gebaut und nicht
  nach `VmPortal.Api/wwwroot` kopiert - das bleibt ein separater manueller Schritt (siehe
  README.md, Abschnitt "Produktions-Build und Auslieferung").
- **Kein echter Health-Endpoint.** Der Check gegen `http://localhost:<Port>/` prüft nur, ob
  der Prozess überhaupt antwortet, nicht ob z. B. die Datenbankverbindung funktioniert oder
  Hyper-V erreichbar ist.
