<#
.SYNOPSIS
    Deployt VmPortal auf dieser Maschine: git pull, EF-Core-Migration, Neubau, Neustart, Health-Check.

.DESCRIPTION
    Für WIEDERHOLTE Deployments auf der Produktions-VM gedacht - kein Erstclone (das Repo muss
    bereits unter -RepoPath geklont sein), kein automatisches Rollback, kein CI/CD-Trigger,
    keine Registrierung als Windows-Dienst (siehe TODO bei Schritt "Prozess stoppen" und
    docs/deployment.md).

    Muss AUF der Produktions-VM (Windows Server) manuell per PowerShell ausgeführt werden -
    läuft nicht automatisch und nicht von einem CI-System oder von Claude Code aus.

    Reihenfolge weicht an einer Stelle bewusst von einer rein linearen "erst migrieren, dann
    stoppen"-Lesart ab: Der laufende Prozess wird VOR der Migration und VOR dem Publish
    gestoppt, nicht erst danach - sowohl die SQLite-Datenbankdatei als auch die DLLs im
    Publish-Verzeichnis dürfen beim Migrieren bzw. Publishen nicht von einem noch laufenden
    Prozess gesperrt sein. Migration und Publish laufen also beide erst NACH dem Stopp, aber
    beide immer noch VOR dem Neustart der Anwendung - das eigentlich geforderte Verhalten
    ("Migration vor dem Neustart") ist damit erfüllt.

.PARAMETER Branch
    Git-Branch, der deployt werden soll.

.PARAMETER RepoPath
    Pfad zum bereits geklonten Repo auf dieser Maschine.

.PARAMETER PublishPath
    Zielverzeichnis für "dotnet publish". Wird bei jedem Lauf komplett geleert, damit keine
    Datei-Leichen aus früheren Versionen liegen bleiben (dotnet publish räumt das Zielverzeichnis
    von sich aus nicht auf).

.PARAMETER Port
    Port, auf dem die Anwendung nach dem Neustart lauscht (--urls http://0.0.0.0:<Port>).

.PARAMETER HealthCheckTimeoutSeconds
    Wie lange nach dem Start insgesamt auf eine Antwort gewartet wird, bevor der Health-Check
    als fehlgeschlagen gilt.

.PARAMETER HealthCheckIntervalSeconds
    Wartezeit zwischen zwei Health-Check-Versuchen.

.EXAMPLE
    .\deploy.ps1 -Branch main

.EXAMPLE
    .\deploy.ps1 -Branch main -Port 5000 -RepoPath C:\VmPortal\vm-portal
#>
[CmdletBinding()]
param(
    [string]$Branch = "main",
    [string]$RepoPath = "C:\VmPortal\vm-portal",
    [string]$PublishPath = "C:\VmPortal\publish",
    [int]$Port = 5000,
    [int]$HealthCheckTimeoutSeconds = 30,
    [int]$HealthCheckIntervalSeconds = 2
)

# Wirkt nur auf PowerShell-Cmdlets (z. B. Invoke-WebRequest, Remove-Item), NICHT auf externe
# Programme wie git/dotnet - deren Fehlschläge werden unten explizit über $LASTEXITCODE geprüft.
$ErrorActionPreference = "Stop"

$scriptStart = Get-Date

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-SubStep {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Exit-WithStepFailure {
    param(
        [Parameter(Mandatory)][string]$StepName,
        [Parameter(Mandatory)][string]$Detail
    )
    Write-Host ""
    Write-Host "FEHLER in Schritt '$StepName':" -ForegroundColor Red
    Write-Host "  $Detail" -ForegroundColor Red
    exit 1
}

function Invoke-ExternalCommand {
    <#
    Führt ein externes Kommando (git/dotnet) aus und bricht das Skript mit einer eindeutig
    dem Schritt zugeordneten Fehlermeldung ab, falls der Exit-Code ungleich 0 ist. Notwendig,
    weil PowerShell bei fehlgeschlagenen externen Prozessen selbst keine terminierende
    Exception wirft - ein einfaches try/catch würde einen fehlgeschlagenen "git pull" oder
    "dotnet ef database update" sonst stillschweigend übergehen.
    #>
    param(
        [Parameter(Mandatory)][string]$StepName,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList
    )
    Write-SubStep "$FilePath $($ArgumentList -join ' ')"
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        Exit-WithStepFailure -StepName $StepName -Detail "Exit-Code $LASTEXITCODE. Siehe Ausgabe oberhalb dieser Meldung."
    }
}

# =============================================================================
# Schritt 1: Repo-Prüfung - kein automatischer Clone, nur wiederholte Deployments
# =============================================================================
Write-Step "Pruefe Git-Repository unter '$RepoPath'"

if (-not (Test-Path $RepoPath)) {
    Exit-WithStepFailure -StepName "Repo-Pruefung" `
        "Verzeichnis '$RepoPath' existiert nicht. deploy.ps1 clont nicht automatisch - bitte das Repo einmalig manuell klonen, siehe docs/deployment.md."
}

if (-not (Test-Path (Join-Path $RepoPath ".git"))) {
    Exit-WithStepFailure -StepName "Repo-Pruefung" `
        "'$RepoPath' ist kein Git-Repository (kein .git-Unterordner gefunden). deploy.ps1 clont nicht automatisch - bitte das Repo einmalig manuell klonen, siehe docs/deployment.md."
}

Write-SubStep "OK: '$RepoPath' ist ein Git-Repository."

# =============================================================================
# Schritt 2: git fetch + git pull auf dem Ziel-Branch
# =============================================================================
Write-Step "Git fetch (origin)"
Invoke-ExternalCommand -StepName "Git Fetch" -FilePath "git" -ArgumentList @("-C", $RepoPath, "fetch", "origin")

Write-Step "Git checkout '$Branch'"
Invoke-ExternalCommand -StepName "Git Checkout" -FilePath "git" -ArgumentList @("-C", $RepoPath, "checkout", $Branch)

Write-Step "Git pull (origin/$Branch)"
# Bei Merge-Konflikten liefert "git pull" einen Exit-Code != 0 zurueck und die Konfliktdetails
# stehen bereits in Gits eigener Ausgabe direkt oberhalb der folgenden Fehlermeldung.
Invoke-ExternalCommand -StepName "Git Pull" -FilePath "git" -ArgumentList @("-C", $RepoPath, "pull", "origin", $Branch)

# =============================================================================
# Schritt 3: laufenden VmPortal-Prozess stoppen
#   Bewusst hier (vor Migration/Publish) statt erst kurz vor dem Neustart - Begruendung s.o.
#   TODO: Sobald VmPortal als Windows-Dienst registriert ist, diesen Abschnitt durch
#   "Stop-Service VmPortal" ersetzen statt per Kommandozeile nach dem Prozess zu suchen.
# =============================================================================
Write-Step "Suche laufenden VmPortal-Prozess (VmPortal.Api.dll)"

$runningProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction Stop |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*VmPortal.Api.dll*" }

if ($runningProcesses) {
    foreach ($proc in $runningProcesses) {
        Write-SubStep "Stoppe PID $($proc.ProcessId): $($proc.CommandLine)"
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
        } catch {
            Exit-WithStepFailure -StepName "Prozess stoppen" "Konnte Prozess PID $($proc.ProcessId) nicht beenden: $($_.Exception.Message)"
        }
    }
    # Kurze Wartezeit, damit Windows Datei-Handles (SQLite-Datei, DLLs) tatsaechlich freigibt.
    Start-Sleep -Seconds 2
    Write-SubStep "Alle gefundenen VmPortal-Prozesse gestoppt."
} else {
    Write-SubStep "Kein laufender VmPortal-Prozess gefunden - ueberspringe."
}

# =============================================================================
# Schritt 4: EF-Core-Migration gegen appsettings.Production.json
#   ASPNETCORE_ENVIRONMENT=Production sorgt dafuer, dass "dotnet ef" beim Aufbau des
#   Design-Time-Hosts appsettings.Production.json (ConnectionStrings:VmPortalDb) auflöst -
#   dieselbe Konfigurationsaufloesung wie die Anwendung selbst zur Laufzeit, keine
#   Duplizierung des Connection-Strings im Skript.
# =============================================================================
Write-Step "Datenbank-Migration (dotnet ef database update)"

$env:ASPNETCORE_ENVIRONMENT = "Production"
$coreProject = Join-Path $RepoPath "VmPortal.Core"
$apiProject = Join-Path $RepoPath "VmPortal.Api"

if (-not (Test-Path $coreProject) -or -not (Test-Path $apiProject)) {
    Exit-WithStepFailure -StepName "Datenbank-Migration" `
        "VmPortal.Core oder VmPortal.Api wurden unter '$RepoPath' nicht gefunden - stimmt -RepoPath?"
}

Invoke-ExternalCommand -StepName "Datenbank-Migration" -FilePath "dotnet" -ArgumentList @(
    "ef", "database", "update",
    "--project", $coreProject,
    "--startup-project", $apiProject
)
# Typische Fehlerursachen, falls dieser Schritt scheitert: SQLite-Datei durch einen noch
# laufenden Prozess gesperrt (sollte durch Schritt 3 bereits ausgeschlossen sein), das
# "dotnet-ef"-Tool ist nicht installiert (siehe docs/deployment.md), oder die Migration
# selbst schlägt inhaltlich fehl (z. B. Schema-Konflikt) - jeweils direkt in Gits/dotnets
# eigener Ausgabe oberhalb der Fehlermeldung sichtbar.

# =============================================================================
# Schritt 5: Publish - Zielverzeichnis vorher leeren (keine Datei-Leichen)
#   "dotnet publish" entfernt Dateien frueherer Versionen NICHT selbst, wenn sie im neuen
#   Build nicht mehr vorkommen (z. B. entfernte/umbenannte DLLs) - Microsofts eigene
#   Empfehlung dafuer ist, das Zielverzeichnis vor jedem Publish zu leeren.
# =============================================================================
Write-Step "Publish-Verzeichnis leeren: $PublishPath"

if (Test-Path $PublishPath) {
    try {
        Remove-Item -Path $PublishPath -Recurse -Force -ErrorAction Stop
    } catch {
        Exit-WithStepFailure -StepName "Publish-Verzeichnis leeren" `
            "Konnte '$PublishPath' nicht loeschen: $($_.Exception.Message). Laeuft eventuell noch ein Prozess darauf?"
    }
}

Write-Step "dotnet publish VmPortal.Api (Release)"
Invoke-ExternalCommand -StepName "Publish" -FilePath "dotnet" -ArgumentList @(
    "publish", $apiProject,
    "-c", "Release",
    "-o", $PublishPath
)

# =============================================================================
# Schritt 6: Anwendung neu starten
# =============================================================================
Write-Step "Starte VmPortal.Api auf http://0.0.0.0:$Port"

$dllPath = Join-Path $PublishPath "VmPortal.Api.dll"
if (-not (Test-Path $dllPath)) {
    Exit-WithStepFailure -StepName "Anwendung starten" "'$dllPath' existiert nicht - der Publish-Schritt scheint fehlgeschlagen zu sein."
}

$stdOutLog = Join-Path $PublishPath "vmportal.stdout.log"
$stdErrLog = Join-Path $PublishPath "vmportal.stderr.log"

try {
    Start-Process -FilePath "dotnet" `
        -ArgumentList @($dllPath, "--urls", "http://0.0.0.0:$Port") `
        -WorkingDirectory $PublishPath `
        -RedirectStandardOutput $stdOutLog `
        -RedirectStandardError $stdErrLog `
        -WindowStyle Hidden `
        -ErrorAction Stop
} catch {
    Exit-WithStepFailure -StepName "Anwendung starten" "Start-Process ist fehlgeschlagen: $($_.Exception.Message)"
}

Write-SubStep "Prozess gestartet. stdout: $stdOutLog / stderr: $stdErrLog"

# =============================================================================
# Schritt 7: Health-Check mit Timeout/Retry
#   Kein dedizierter Health-Endpoint vorhanden (TODO: z. B. "/healthz" in VmPortal.Api
#   ergaenzen). Bis dahin zaehlt jede HTTP-Antwort - auch ein 404 - als "Kestrel läuft und
#   nimmt Verbindungen an"; nur eine Verbindungsverweigerung/ein Timeout gilt als Fehlschlag.
# =============================================================================
Write-Step "Health-Check: http://localhost:$Port/"

$healthUrl = "http://localhost:$Port/"
$deadline = (Get-Date).AddSeconds($HealthCheckTimeoutSeconds)
$healthOk = $false
$lastError = $null
$lastStatusCode = $null

while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        $lastStatusCode = [int]$response.StatusCode
        $healthOk = $true
        break
    } catch {
        $ex = $_.Exception
        # Manche Nicht-2xx-Antworten (z. B. 404, falls kein SPA-Build in wwwroot liegt)
        # loesen in PowerShell eine Exception aus, obwohl der Server geantwortet hat -
        # das zaehlt fuer den Zweck dieses Checks trotzdem als "erreichbar".
        if ($ex.Response -and $ex.Response.StatusCode) {
            $lastStatusCode = [int]$ex.Response.StatusCode
            $healthOk = $true
            break
        }
        $lastError = $ex.Message
    }
    Start-Sleep -Seconds $HealthCheckIntervalSeconds
}

if (-not $healthOk) {
    Exit-WithStepFailure -StepName "Health-Check" `
        "Keine Antwort von '$healthUrl' innerhalb von $HealthCheckTimeoutSeconds Sekunden. Letzter Fehler: $lastError. Log-Dateien pruefen: $stdOutLog / $stdErrLog"
}

Write-SubStep "OK: HTTP $lastStatusCode von $healthUrl"

# =============================================================================
# Schritt 8: Zusammenfassung
# =============================================================================
$commitHash = (git -C $RepoPath rev-parse --short HEAD).Trim()
$duration = (Get-Date) - $scriptStart

Write-Host ""
Write-Host "===================== Deployment abgeschlossen =====================" -ForegroundColor Green
Write-Host "Branch:        $Branch"
Write-Host "Commit:        $commitHash"
Write-Host "Zeitstempel:   $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host "Dauer:         $([math]::Round($duration.TotalSeconds, 1)) s"
Write-Host "Health-Check:  OK (HTTP $lastStatusCode von $healthUrl)"
Write-Host "======================================================================" -ForegroundColor Green
