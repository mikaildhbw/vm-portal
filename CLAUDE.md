# VmPortal — Projektkontext für Claude Code

## Über das Projekt
Self-Service-Portal für virtuelle Maschinen (Hyper-V), Bachelorarbeit bei der Siemens AG.

**Forschungsfrage:** Wie wird ein sicheres Self-Service-Portal für virtuelle Maschinen
konzipiert, implementiert und hinsichtlich Sicherheit, Benutzerfreundlichkeit und
Plattformunabhängigkeit bewertet?

## Architektur
Zwei Projekte, klare Schichtentrennung:

- **VmPortal.Api** — REST-API (ASP.NET Core 8): Controller, Middleware, DI, Authentifizierung.
- **VmPortal.Core** — Fachlogik: Interfaces, Models, Services, Konfigurationsklassen.

```
VmPortal.Core/
  Interfaces/     IVirtualizationProvider, IAuthService, ITokenService
  Models/         VirtualMachine, VmStatus
  Services/       HyperVProvider, DummyVirtualizationProvider,
                  LdapAuthService, JwtTokenService, VirtualizationException
  Configuration/  HyperVSettings, LdapSettings, JwtSettings
VmPortal.Api/
  Controllers/    AuthController, VmController
  Middleware/     VirtualizationExceptionMiddleware
  Constants/      AuthConstants
  Program.cs      Composition Root (DI, Auth, Provider-Auswahl)
```

### Zentrale Abstraktion
`IVirtualizationProvider` kapselt den Hypervisor. Konkrete Implementierungen:
- `HyperVProvider` — Microsoft Hyper-V über PowerShell-Remoting (WinRM/HTTPS).
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

## Testumgebung (nur Entwicklung — keine Produktions-Secrets)
- Windows Server 2022 als KVM-Gast auf Ubuntu, IP `192.168.122.196`.
- AD-Domäne `testumgebung.local`, LDAP auf Port 389.
- WinRM-HTTPS-Listener auf Port 5986 (selbstsigniertes Zertifikat).
- Testbenutzer: `mugur` / `Test1234!` und `jburath` / `Test1234!`
  (Gruppe `VM-Portal-Benutzer`, Rolle `VMUser`).

## Was bereits implementiert ist (M1–M4)
- **M1/M2:** Projektstruktur, Interfaces, Models, Dummy-Services, DI, testbare API.
- **M3:** LDAP-Authentifizierung gegen AD; Rolle aus AD-Gruppenmitgliedschaft; echtes JWT
  (HMAC-SHA256, Claims `username`/`role`, 8 h); JWT im `httpOnly`-Cookie
  (`SameSite=Strict`, `Secure`); JWT-Bearer-Middleware liest Token aus dem Cookie; alle
  Endpunkte außer `/api/auth/login` sind mit `[Authorize]` geschützt; RBAC im `VmController`
  (nur eigene VMs, `403` bei fremder VM).
- **M4:** `HyperVProvider` über PowerShell-Remoting (`Get-VM`, `Start-VM`, `Stop-VM -Force`,
  `Restart-VM -Force`, `Checkpoint-VM`); konfigurierbare Provider-Auswahl;
  `VirtualizationExceptionMiddleware` liefert bei Hyper-V-/WinRM-Fehlern einen sprechenden
  `502` statt `500`; Logging aller Aktionen.

## Was noch kommt (Phase 5–7)
- **Phase 5 — Persistenz:** Datenbank (z. B. PostgreSQL/EF Core) für die persistente
  Zuordnung VM ↔ Benutzer sowie Audit-Log. Secrets aus `appsettings.json` in
  Umgebungsvariablen/Secret-Store auslagern.
- **Phase 6 — Frontend:** Web-Oberfläche (kein Blazor; ASP.NET-konforme Alternative) mit
  Cookie-Auth-Flow.
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
- **Fehlerübersetzung via Middleware:** Infrastrukturfehler (WinRM nicht erreichbar) werden
  als `502` mit Klartextmeldung sichtbar, fachliche Fehler bleiben `403`/`404` — saubere
  Trennung der Fehlersemantik.
- **PowerShell-Remoting statt nativer Hyper-V-.NET-API:** Die Hyper-V-Cmdlets sind die
  offiziell unterstützte, stabile Automatisierungsschnittstelle und funktionieren remote über
  WinRM; die App läuft auf Windows Server, wo der WSMan-Client nativ vorhanden ist.

## Konventionen und Constraints
- ASP.NET Core 8, C#. Kein Blazor, kein Django, kein Python.
- Entwicklung unter Ubuntu, Ausführung auf Windows Server 2022 — Windows-APIs sind erlaubt,
  der Build muss aber plattformübergreifend fehlerfrei sein.
- Sämtliche Secrets/Verbindungsdaten in `appsettings.json`, nichts hartcodiert.
- Keine Breaking Changes an `IVirtualizationProvider`.
- Nach jeder abgeschlossenen Aufgabe committen (Conventional Commits, deutschsprachige
  Beschreibung); `dotnet build VmPortal.sln` muss grün sein.

## Bekannte Einschränkung beim Testen
Der WSMan-Client ist nur unter Windows zur Laufzeit verfügbar. Unter Linux baut und startet die
App, der `HyperV`-Provider quittiert einen Zugriff aber planmäßig mit `502` (WSMan-Client
fehlt). Ein vollständiger Hyper-V-End-to-End-Test läuft daher auf dem Windows-Server-Deployment.
