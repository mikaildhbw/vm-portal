# Diagramme (PlantUML)

Alle Diagramme der Bachelorarbeit liegen als PlantUML-Quelltext in diesem Verzeichnis
(`*.puml`) und werden als PDF nach `rendered/` gerendert. Die PDFs werden im LaTeX-Dokument
per `\includegraphics` eingebunden (siehe `../bachelorarbeit.tex`).

## Voraussetzungen

- Java (OpenJDK 17+, getestet mit OpenJDK 21)
- `plantuml.jar`, abgelegt unter `../tools/plantuml.jar` (nicht versioniert, siehe unten)
- Graphviz (`dot`) für das automatische Layout von Klassen-, Komponenten-, Use-Case- und
  Deploymentdiagrammen. Sequenzdiagramme benötigen kein Graphviz.

Ohne Root-Rechte lässt sich Graphviz z. B. per Conda installieren:

```bash
conda install -c conda-forge graphviz
```

Liegt `dot` nicht im `PATH` (z. B. bei einer Conda-Installation), muss der Pfad zur eigenen
`dot`-Installation beim Rendern explizit über die JVM-Property `-DGRAPHVIZ_DOT` übergeben
werden (siehe unten) — ermitteln lässt er sich z. B. via `which dot`. Ist Graphviz systemweit
installiert und über `PATH` auffindbar, kann die Property entfallen.

## plantuml.jar besorgen

```bash
mkdir -p thesis/tools
curl -fL -o thesis/tools/plantuml.jar \
  "https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar"
```

## Neu rendern

Einzelnes Diagramm:

```bash
java -DGRAPHVIZ_DOT="$(which dot)" -jar thesis/tools/plantuml.jar \
  -tpdf thesis/diagrams/<name>.puml -o rendered
```

Alle Diagramme auf einmal (aus dem Repository-Wurzelverzeichnis):

```bash
for f in thesis/diagrams/*.puml; do
  java -DGRAPHVIZ_DOT="$(which dot)" -jar thesis/tools/plantuml.jar \
    -tpdf "$f" -o rendered
done
```

Die Ausgabe landet jeweils unter `thesis/diagrams/rendered/<name>.pdf`.

Zur schnellen Sichtprüfung während der Bearbeitung eignet sich PNG-Export
(`-tpng` statt `-tpdf`), das aber nicht eingecheckt bzw. eingebunden wird —
für das LaTeX-Dokument ist ausschließlich das PDF relevant (verlustfreier Vektor-Export).

## Diagrammübersicht

| Datei | Inhalt | Kapitel |
|---|---|---|
| `architektur_uebersicht.puml` | 4-Schichten-Komponentendiagramm, `IVirtualizationProvider` hervorgehoben | 5.2 Hypervisor-Abstraktion |
| `use_case_rollen.puml` | Use-Cases je VM-Rolle (`VmRole`/`VmAction`/`RolePermissions`) | 5.3 Sicherheitskonzept |
| `klassendiagramm_auth.puml` | Klassendiagramm Authentifizierung & Autorisierung (`VmPortal.Core`) | 6.1 Projektstruktur |
| `klassendiagramm_virtualisierung.puml` | Klassendiagramm Virtualisierungsschicht (`VmPortal.Core`) | 6.1 Projektstruktur |
| `er_diagramm_autorisierung.puml` | Implementierte SQLite-Autorisierungsschicht (RBAC) | 5.3 Sicherheitskonzept |
| `sequenz_login.puml` | Ablauf Login/Authentifizierung inkl. Fehlerfall | 5.3 Sicherheitskonzept |
| `sequenz_vm_aktion_autorisierung.puml` | Ablauf einer VM-Aktion inkl. Autorisierungsprüfung | 5.3 Sicherheitskonzept |
| `deployment_umgebungen.puml` | Entwicklungs- und Zielumgebung (Siemens) | 8.2 Weg zur Produktionsreife |

## Layout-Hinweise

Alle Diagramme werden mit Graphviz (`dot`) statt einer reinen Java-Fallback-Engine
gerendert; das ergibt spürbar sauberere Kantenführung und Platzierung, insbesondere beim
Komponenten-, ER- und Deploymentdiagramm. Eine Ausnahme: In `use_case_rollen.puml` wurde
die ursprünglich am Use-Case „VM exportieren/importieren/klonen" verankerte Erklärungs-Notiz
entfernt, weil Graphviz sie mit einer sehr langen, quer durchs Diagramm laufenden
Verbindungslinie gerendert hat. Der entsprechende Hinweistext (Gesamtzahl der Aktionen im
`VmAction`-Enum) steht stattdessen in der Bildunterschrift in `bachelorarbeit.tex`.

Alle Diagramme folgen einer einheitlichen, dezenten Farbgebung (neutrales Grau für
Strukturelemente, ein gedämpftes Teal als alleiniger Akzent für die jeweils zentrale
Abstraktion bzw. den WinRM-Pfad) statt der zuvor uneinheitlichen Grün-/Blau-/Gelbtöne, und
sind auf das für das Verständnis Wesentliche reduziert — vollständige Attribut-/Methoden-
bzw. Aktionslisten stehen im Fließtext oder in Tabellen, nicht in den Diagrammen. Das
ursprünglich einzelne Klassendiagramm (17 Klassen/Enums) ist seit der Layout-Überarbeitung
in `klassendiagramm_auth.puml` (Authentifizierung & Autorisierung) und
`klassendiagramm_virtualisierung.puml` (Virtualisierungsschicht) aufgeteilt.

## Hinweis zur Genauigkeit

Alle Diagramme in diesem Verzeichnis, einschließlich `er_diagramm_autorisierung.puml`,
bilden den tatsächlichen Code in `VmPortal.Core`/`VmPortal.Api` ab (Stand siehe
Git-Historie dieses Verzeichnisses). `er_diagramm_autorisierung.puml` zeigte in einer
früheren Fassung ein noch nicht implementiertes Zielmodell (Soll-Konzept); seit der
SQLite-Autorisierungsschicht (Commits `76b7cef`, `7df0aae`) ist es der implementierte
Datenbankstand. `sequenz_vm_aktion_autorisierung.puml` wurde im selben Zug von der
vmroles-Claim-Prüfung auf `DbAuthorizationService` umgestellt. **Bekannte, noch nicht
behobene Ausnahme:** `architektur_uebersicht.puml` zeigt weiterhin `VmController` mit einer
direkten Abhängigkeit zu `RolePermissions.IsAllowed(role, action)` — diese Prüfung wurde im
Zuge der RBAC-Umstellung aus `VmController` entfernt (die Autorisierung läuft heute über
`DbAuthorizationService`, siehe `sequenz_vm_aktion_autorisierung.puml`). Bei der reinen
Layout-Überarbeitung dieses Diagramms wurde das bewusst nicht mitkorrigiert (Auftrag war
Layout, keine inhaltliche Korrektur) und bleibt als offener Punkt für die nächste
inhaltliche Konsolidierung.
