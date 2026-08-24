<#
.SYNOPSIS
    Automatisiert die neun Sicherheits-Prüfszenarien aus Kapitel 7.2 der Bachelorarbeit
    (Abschnitt "Sicherheit") gegen eine laufende VmPortal-Instanz und protokolliert die
    Ergebnisse strukturiert.

.DESCRIPTION
    Bildet Authentifizierung (FA-01), Autorisierung (FA-03/FA-04), Token-Speicherung/
    Cookie-Attribute (NFA-02) und Fehlersemantik (FA-13) aus dem Kapitel-7-Text als
    HTTP-Testfälle gegen AuthController, VmController und den Admin-Endpunkt
    /api/admin/discover-vms ab (siehe VmPortal.Api/Controllers).

    Ausschließlich lesende bzw. von vornherein autorisierungsblockierte Aufrufe: Testfall 5
    (nicht zugewiesene VM-Aktion) nutzt bewusst GET /api/vm/{id} (Aktion ViewDetails) statt
    einer state-changing Aktion wie /start, weil VmController.AuthorizeVmActionAsync die
    Berechtigung IMMER prüft, bevor der Provider aufgerufen wird (siehe VmController.cs,
    ExecuteVmActionAsync/AuthorizeVmActionAsync) - ein GET auf eine fremde VM ist damit
    genauso aussagekräftig für den 403-Test wie ein POST /start, aber ohne jedes Risiko einer
    tatsächlichen Zustandsänderung, selbst bei einer fehlerhaften Parametrisierung dieses
    Skripts. Aus demselben Grund enthält dieses Skript keinen einzigen POST auf
    /start|/stop|/restart o.ä. gegen echte Produktions-VMs.

    Falls eine künftige Erweiterung dieses Skripts tatsächlich eine state-changing VM-Aktion
    benötigt, DARF diese ausschließlich gegen die Test-VMs HVP_1 bis HVP_9 laufen (siehe
    Variable $AllowedStateChangingTestVmIds und Hilfsfunktion Assert-SafeForStateChange weiter
    unten) - niemals gegen produktive Kunden-VMs.

    Zugangsdaten werden ausschließlich als Laufzeit-Parameter entgegengenommen (SecureString)
    und nirgends persistiert: weder im Skript, noch in der Konsolen-/Markdown-Ausgabe, noch in
    einer Zwischendatei.

.PARAMETER BaseUrl
    Basis-URL der laufenden VmPortal-Instanz, z. B. https://vmportal.example.com (ohne
    abschließenden Slash).

.PARAMETER Username
    AD-Benutzername eines gültigen, NICHT FullAdmin-berechtigten Testbenutzers (Testfall 1, 3-8).

.PARAMETER Password
    Passwort zu -Username, als SecureString. Wird nur im Arbeitsspeicher gehalten.

.PARAMETER InvalidPassword
    Bewusst falsches Passwort für Testfall 2 (ungültige Zugangsdaten). Optional - Default ist
    ein fester, garantiert falscher Platzhalterwert; -Username bleibt dabei unverändert gültig,
    sodass ausschließlich das falsche Passwort den 401 auslöst.

.PARAMETER OtherUserVmId
    VM-Id/-Name einer VM, die NICHT über die GroupPermissions von -Username autorisiert ist
    (Testfall 5, erwartet 403). Ohne Angabe wird Testfall 5 als SKIPPED protokolliert.

.PARAMETER NonExistentVmId
    VM-Id, die im Hypervisor-Inventar nicht existiert (Testfall 6, erwartet 404). Default: ein
    pro Lauf eindeutiger, offensichtlich nicht existierender Bezeichner.

.PARAMETER AdminEndpoint
    Admin-Endpunkt für Testfall 7 (FullAdmin-only, erwartet 403 mit einem Nicht-FullAdmin-
    Token). Default: /api/admin/discover-vms.

.PARAMETER HypervisorUnreachableVmId
    Optionale VM-Id, die bekanntermaßen auf einen nicht erreichbaren Hypervisor-Host zeigt
    (Testfall 9, erwartet 502 über VirtualizationExceptionMiddleware). Ohne Angabe wird
    Testfall 9 als MANUAL protokolliert, siehe Kapitel-7-Text ("falls simulierbar, sonst als
    manueller Schritt markieren").

.PARAMETER OutputMarkdownPath
    Zielpfad der Markdown-Ergebnisdatei. Default: security-testfaelle-ergebnis.md im selben
    Verzeichnis wie dieses Skript.

.PARAMETER AllowInsecureSsl
    Deaktiviert die TLS-Zertifikatsprüfung für diesen Lauf (nur für Testumgebungen mit
    selbstsigniertem Zertifikat gedacht - siehe Abweichung Test-/Produktivumgebung in
    Kapitel 7.1). In der echten Produktivumgebung NICHT setzen.

.EXAMPLE
    $pw = Read-Host -AsSecureString "Passwort"
    .\security-testfaelle.ps1 -BaseUrl https://vmportal.example.com -Username jburath `
        -Password $pw -OtherUserVmId "VM-Mikail" -HypervisorUnreachableVmId ""

.EXAMPLE
    .\security-testfaelle.ps1 -BaseUrl https://vmportal.example.com -Username jburath `
        -Password (Read-Host -AsSecureString "Passwort") -OtherUserVmId "VM-Fremd" `
        -NonExistentVmId "does-not-exist-123"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [securestring]$Password,

    [securestring]$InvalidPassword,

    [string]$OtherUserVmId,

    [string]$NonExistentVmId = "thesis-test-nonexistent-vm-$([guid]::NewGuid().ToString('N').Substring(0,8))",

    [string]$AdminEndpoint = "/api/admin/discover-vms",

    [string]$HypervisorUnreachableVmId,

    [string]$OutputMarkdownPath = (Join-Path $PSScriptRoot "security-testfaelle-ergebnis.md"),

    [switch]$AllowInsecureSsl
)

$ErrorActionPreference = "Stop"

# --- Sicherheitsleitplanke: state-changing Aktionen nur gegen diese Test-VMs -----------------
# Wird von diesem Skript aktuell an keiner Stelle tatsächlich für einen POST /start|/stop
# benutzt (siehe .DESCRIPTION) - dient als Guard, falls das Skript künftig um einen echten
# state-changing Testfall erweitert wird.
$AllowedStateChangingTestVmIds = @("HVP_1", "HVP_2", "HVP_3", "HVP_4", "HVP_5", "HVP_6", "HVP_7", "HVP_8", "HVP_9")

function Assert-SafeForStateChange {
    param([Parameter(Mandatory = $true)][string]$VmId)
    if ($AllowedStateChangingTestVmIds -notcontains $VmId) {
        throw "Sicherheitsleitplanke: state-changing VM-Aktionen sind in diesem Testskript " +
            "ausschließlich gegen $($AllowedStateChangingTestVmIds -join ', ') erlaubt, nicht gegen '$VmId'."
    }
}

# --- TLS / Zertifikatsbehandlung --------------------------------------------------------------
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    if ($AllowInsecureSsl) {
        [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    }
}

$script:SkipCertCheckParam = @{}
if ($AllowInsecureSsl -and $PSVersionTable.PSVersion.Major -ge 6) {
    $script:SkipCertCheckParam = @{ SkipCertificateCheck = $true }
}

# --- Hilfsfunktionen -----------------------------------------------------------------------

function ConvertTo-PlainText {
    param([securestring]$Secure)
    if (-not $Secure) { return $null }
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

<#
    Führt einen HTTP-Aufruf aus und liefert IMMER ein Ergebnisobjekt mit Statuscode zurück -
    auch bei 4xx/5xx, die Invoke-WebRequest standardmäßig als Exception wirft. Damit
    funktioniert dasselbe Skript unter Windows PowerShell 5.1 (Response ist
    System.Net.HttpWebResponse) und PowerShell 7+ (Response ist
    System.Net.Http.HttpResponseMessage) ohne Versionsverzweigung im Testfall-Code.
#>
function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers = @{},
        [string]$Body
    )

    $params = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = $Headers
        ErrorAction = "Stop"
    } + $script:SkipCertCheckParam

    if ($PSBoundParameters.ContainsKey("Body")) {
        $params.Body = $Body
        $params.ContentType = "application/json"
    }

    try {
        $response = Invoke-WebRequest @params
        return [PSCustomObject]@{
            StatusCode   = [int]$response.StatusCode
            Headers      = $response.Headers
            Content      = $response.Content
            ErrorMessage = $null
        }
    } catch {
        $statusCode = $null
        $errResponse = $_.Exception.Response
        if ($errResponse -and ($errResponse.PSObject.Properties.Name -contains "StatusCode")) {
            try { $statusCode = [int]$errResponse.StatusCode } catch { $statusCode = $null }
        }
        return [PSCustomObject]@{
            StatusCode   = $statusCode
            Headers      = $null
            Content      = $_.ErrorDetails.Message
            ErrorMessage = $_.Exception.Message
        }
    }
}

function Get-RawSetCookieHeader {
    param($ApiResponse)
    if (-not $ApiResponse.Headers) { return $null }
    $value = $ApiResponse.Headers["Set-Cookie"]
    if (-not $value) { return $null }
    if ($value -is [array]) { return ($value -join "`n") }
    return [string]$value
}

function Get-JwtFromSetCookie {
    param([string]$RawSetCookie)
    if (-not $RawSetCookie) { return $null }
    if ($RawSetCookie -match "jwt=([^;]+)") { return $Matches[1] }
    return $null
}

$script:Results = @()

function Add-TestResult {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Expected,
        [string]$Actual,
        [Parameter(Mandatory = $true)][ValidateSet("PASS", "FAIL", "SKIPPED", "MANUAL")][string]$Result,
        [string]$Details = ""
    )

    $entry = [PSCustomObject]@{
        Id        = $Id
        Name      = $Name
        Expected  = $Expected
        Actual    = $Actual
        Result    = $Result
        Timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        Details   = $Details
    }
    $script:Results += $entry

    $color = switch ($Result) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIPPED" { "Yellow" }
        "MANUAL" { "Cyan" }
    }
    Write-Host ("[{0}] {1} - {2} (erwartet: {3}, ist: {4})" -f $entry.Timestamp, $Id, $Result, $Expected, $Actual) -ForegroundColor $color
    if ($Details) { Write-Host "    $Details" -ForegroundColor DarkGray }
}

function Test-StatusExpectation {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][int]$ExpectedStatus,
        [Parameter(Mandatory = $true)]$ApiResponse,
        [string]$Details = ""
    )

    $actual = if ($null -eq $ApiResponse.StatusCode) { "keine Antwort" } else { [string]$ApiResponse.StatusCode }
    $result = if ($ApiResponse.StatusCode -eq $ExpectedStatus) { "PASS" } else { "FAIL" }
    if ($ApiResponse.ErrorMessage -and $result -eq "FAIL") {
        $Details = if ($Details) { "$Details | $($ApiResponse.ErrorMessage)" } else { $ApiResponse.ErrorMessage }
    }
    Add-TestResult -Id $Id -Name $Name -Expected ([string]$ExpectedStatus) -Actual $actual -Result $result -Details $Details
}

$BaseUrl = $BaseUrl.TrimEnd("/")
$plainPassword = ConvertTo-PlainText -Secure $Password
$plainInvalidPassword = if ($InvalidPassword) { ConvertTo-PlainText -Secure $InvalidPassword } else { "ThesisInvalidTest-Passwort-9f2c" }

Write-Host "=== Sicherheits-Testfälle Kapitel 7.2 - VmPortal ($BaseUrl) ===" -ForegroundColor White
Write-Host "Testbenutzer: $Username (erwartet: kein FullAdmin)" -ForegroundColor White
Write-Host ""

# --- Testfall 1: Login mit gültigen Zugangsdaten -> 200 + gültiges Cookie -------------------
$validLoginBody = @{ username = $Username; password = $plainPassword } | ConvertTo-Json
$validLoginResponse = Invoke-ApiRequest -Method Post -Uri "$BaseUrl/api/auth/login" -Body $validLoginBody
$rawSetCookie = Get-RawSetCookieHeader -ApiResponse $validLoginResponse
$jwt = Get-JwtFromSetCookie -RawSetCookie $rawSetCookie

$tf1Details = if ($jwt) { "jwt-Cookie in Set-Cookie-Header vorhanden" } else { "KEIN jwt-Cookie im Set-Cookie-Header gefunden" }
$tf1Result = if ($validLoginResponse.StatusCode -eq 200 -and $jwt) { "PASS" } else { "FAIL" }
Add-TestResult -Id "TF1" -Name "Login mit gültigen AD-Zugangsdaten" -Expected "200 + gültiges Cookie" `
    -Actual "$($validLoginResponse.StatusCode)$(if ($jwt) { ' + Cookie vorhanden' } else { ' + Cookie fehlt' })" `
    -Result $tf1Result -Details $tf1Details

# Klartext-Passwort so früh wie möglich aus dem Speicher entfernen.
$plainPassword = $null

if (-not $jwt) {
    Write-Warning "Kein gültiges JWT aus Testfall 1 erhalten - Testfälle 5-8, die eine authentifizierte Sitzung benötigen, werden übersprungen."
}

# --- Testfall 2: Login mit ungültigen Zugangsdaten -> 401 -----------------------------------
$invalidLoginBody = @{ username = $Username; password = $plainInvalidPassword } | ConvertTo-Json
$invalidLoginResponse = Invoke-ApiRequest -Method Post -Uri "$BaseUrl/api/auth/login" -Body $invalidLoginBody
$plainInvalidPassword = $null
Test-StatusExpectation -Id "TF2" -Name "Login mit ungültigen Zugangsdaten" -ExpectedStatus 401 -ApiResponse $invalidLoginResponse

# --- Testfall 3: Zugriff auf /api/vm ohne Cookie -> 401 --------------------------------------
$noCookieResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl/api/vm"
Test-StatusExpectation -Id "TF3" -Name "Zugriff auf /api/vm ohne Cookie" -ExpectedStatus 401 -ApiResponse $noCookieResponse

# --- Testfall 4: Zugriff auf /api/vm mit manipuliertem JWT -> 401 ----------------------------
if ($jwt) {
    $tamperedJwt = $jwt.Substring(0, $jwt.Length - 1) + $(if ($jwt.Substring($jwt.Length - 1) -eq "A") { "B" } else { "A" })
    $tamperedResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl/api/vm" -Headers @{ Cookie = "jwt=$tamperedJwt" }
    Test-StatusExpectation -Id "TF4" -Name "Zugriff auf /api/vm mit manipuliertem JWT" -ExpectedStatus 401 -ApiResponse $tamperedResponse `
        -Details "letztes Zeichen der Signatur verändert"
} else {
    Add-TestResult -Id "TF4" -Name "Zugriff auf /api/vm mit manipuliertem JWT" -Expected "401" -Actual "n/a" -Result "SKIPPED" `
        -Details "Kein gültiges JWT aus Testfall 1 verfügbar"
}

# --- Testfall 5: Zugriff auf eine nicht zugewiesene VM -> 403 --------------------------------
# Bewusst GET (ViewDetails), keine state-changing Aktion - siehe .DESCRIPTION.
if ($jwt -and $OtherUserVmId) {
    $foreignVmResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl/api/vm/$OtherUserVmId" -Headers @{ Cookie = "jwt=$jwt" }
    Test-StatusExpectation -Id "TF5" -Name "Zugriff auf nicht zugewiesene VM ($OtherUserVmId)" -ExpectedStatus 403 -ApiResponse $foreignVmResponse
} else {
    $reason = if (-not $jwt) { "Kein gültiges JWT aus Testfall 1 verfügbar" } else { "-OtherUserVmId nicht angegeben" }
    Add-TestResult -Id "TF5" -Name "Zugriff auf eine nicht zugewiesene VM" -Expected "403" -Actual "n/a" -Result "SKIPPED" -Details $reason
}

# --- Testfall 6: Zugriff auf nicht existierende VM-Id -> 404 --------------------------------
if ($jwt) {
    $missingVmResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl/api/vm/$NonExistentVmId" -Headers @{ Cookie = "jwt=$jwt" }
    Test-StatusExpectation -Id "TF6" -Name "Zugriff auf nicht existierende VM-Id ($NonExistentVmId)" -ExpectedStatus 404 -ApiResponse $missingVmResponse
} else {
    Add-TestResult -Id "TF6" -Name "Zugriff auf nicht existierende VM-Id" -Expected "404" -Actual "n/a" -Result "SKIPPED" `
        -Details "Kein gültiges JWT aus Testfall 1 verfügbar"
}

# --- Testfall 7: Admin-Endpunkt mit Nicht-FullAdmin-Token -> 403 ----------------------------
if ($jwt) {
    $adminResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl$AdminEndpoint" -Headers @{ Cookie = "jwt=$jwt" }
    Test-StatusExpectation -Id "TF7" -Name "Admin-Endpunkt ($AdminEndpoint) mit Nicht-FullAdmin-Token" -ExpectedStatus 403 -ApiResponse $adminResponse `
        -Details "Falls FAIL mit 200: -Username ist FullAdmin, für diesen Testfall ist ein Nicht-FullAdmin-Konto erforderlich"
} else {
    Add-TestResult -Id "TF7" -Name "Admin-Endpunkt mit Nicht-FullAdmin-Token" -Expected "403" -Actual "n/a" -Result "SKIPPED" `
        -Details "Kein gültiges JWT aus Testfall 1 verfügbar"
}

# --- Testfall 8: Cookie-Attribute aus der Login-Antwort (httpOnly, Secure, SameSite=Strict) --
if ($rawSetCookie) {
    $hasHttpOnly = $rawSetCookie -match "(?i)httponly"
    $hasSecure = $rawSetCookie -match "(?i)secure"
    $hasSameSiteStrict = $rawSetCookie -match "(?i)samesite=strict"
    $allPresent = $hasHttpOnly -and $hasSecure -and $hasSameSiteStrict
    $attrSummary = "httpOnly=$hasHttpOnly, Secure=$hasSecure, SameSite=Strict=$hasSameSiteStrict"
    Add-TestResult -Id "TF8" -Name "Cookie-Attribute der Login-Antwort" -Expected "httpOnly + Secure + SameSite=Strict" `
        -Actual $attrSummary -Result $(if ($allPresent) { "PASS" } else { "FAIL" })
} else {
    Add-TestResult -Id "TF8" -Name "Cookie-Attribute der Login-Antwort" -Expected "httpOnly + Secure + SameSite=Strict" `
        -Actual "n/a" -Result "SKIPPED" -Details "Kein Set-Cookie-Header aus Testfall 1 verfügbar"
}

# --- Testfall 9: Nicht erreichbarer Hypervisor-Pfad -> 502 -----------------------------------
if ($jwt -and $HypervisorUnreachableVmId) {
    $unreachableResponse = Invoke-ApiRequest -Method Get -Uri "$BaseUrl/api/vm/$HypervisorUnreachableVmId" -Headers @{ Cookie = "jwt=$jwt" }
    Test-StatusExpectation -Id "TF9" -Name "Nicht erreichbarer Hypervisor-Pfad ($HypervisorUnreachableVmId)" -ExpectedStatus 502 -ApiResponse $unreachableResponse `
        -Details "Erwartete Fehlerquelle: VirtualizationExceptionMiddleware"
} else {
    Add-TestResult -Id "TF9" -Name "Nicht erreichbarer Hypervisor-Pfad" -Expected "502" -Actual "n/a" -Result "MANUAL" `
        -Details "Nicht ohne echten Host-Ausfall sicher simulierbar - manuell durchführen: gezielt einen Hyper-V-Host stoppen/vom Netz trennen, dann GET /api/vm/{id} einer dort gehosteten VM aufrufen und die 502-Antwort prüfen. Siehe Kapitel 7.2."
}

# --- Zusammenfassung -------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Zusammenfassung ===" -ForegroundColor White
$script:Results | Format-Table -Property Id, Name, Expected, Actual, Result, Timestamp -AutoSize

$passCount = ($script:Results | Where-Object { $_.Result -eq "PASS" }).Count
$failCount = ($script:Results | Where-Object { $_.Result -eq "FAIL" }).Count
$skippedCount = ($script:Results | Where-Object { $_.Result -eq "SKIPPED" }).Count
$manualCount = ($script:Results | Where-Object { $_.Result -eq "MANUAL" }).Count
Write-Host "PASS: $passCount | FAIL: $failCount | SKIPPED: $skippedCount | MANUAL: $manualCount" -ForegroundColor White

# --- Markdown-Ausgabe -------------------------------------------------------------------------
$mdLines = New-Object System.Collections.Generic.List[string]
$mdLines.Add("# Ergebnisse der Sicherheits-Testfälle (Kapitel 7.2)")
$mdLines.Add("")
$mdLines.Add("Automatisiert erzeugt durch ``thesis/tests/security-testfaelle.ps1``.")
$mdLines.Add("")
$mdLines.Add("- Ziel-Instanz: ``$BaseUrl``")
$mdLines.Add("- Testbenutzer: ``$Username``")
$mdLines.Add("- Lauf-Zeitpunkt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$mdLines.Add("")
$mdLines.Add("| Nr | Testfall | Erwartet | Ist | Ergebnis | Zeitstempel | Anmerkung |")
$mdLines.Add("|----|----------|----------|-----|----------|-------------|-----------|")
foreach ($r in $script:Results) {
    $detail = $r.Details -replace "\|", "\|"
    $mdLines.Add("| $($r.Id) | $($r.Name) | $($r.Expected) | $($r.Actual) | $($r.Result) | $($r.Timestamp) | $detail |")
}
$mdLines.Add("")
$mdLines.Add("**Zusammenfassung:** PASS: $passCount, FAIL: $failCount, SKIPPED: $skippedCount, MANUAL: $manualCount")
$mdLines.Add("")
$mdLines.Add("Enthält keine Zugangsdaten, Cookies oder Tokens.")

Set-Content -Path $OutputMarkdownPath -Value $mdLines -Encoding UTF8
Write-Host ""
Write-Host "Markdown-Ergebnis geschrieben nach: $OutputMarkdownPath" -ForegroundColor White

# Verbleibende sensible Werte im Speicher aufräumen.
$jwt = $null
