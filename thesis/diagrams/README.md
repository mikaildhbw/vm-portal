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
| `klassendiagramm_core.puml` | Klassendiagramm von `VmPortal.Core` | 6.1 Projektstruktur |
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
`VmAction`-Enum) steht stattdessen in der Bildunterschrift in `bachelorarbeit.tex`. Im
Klassendiagramm (`klassendiagramm_core.puml`) bleibt die Kantendichte bei 17 Klassen/Enums
mit zahlreichen Abhängigkeiten grundsätzlich hoch; das PDF ist ein Vektor-Export und sollte
bei Bedarf am Bildschirm vergrößert werden.

## Hinweis zur Genauigkeit

Alle Diagramme in diesem Verzeichnis, einschließlich `er_diagramm_autorisierung.puml`,
bilden den tatsächlichen Code in `VmPortal.Core`/`VmPortal.Api` ab (Stand siehe
Git-Historie dieses Verzeichnisses). `er_diagramm_autorisierung.puml` zeigte in einer
früheren Fassung ein noch nicht implementiertes Zielmodell (Soll-Konzept); seit der
SQLite-Autorisierungsschicht (Commits `76b7cef`, `7df0aae`) ist es der implementierte
Datenbankstand. `sequenz_vm_aktion_autorisierung.puml` wurde im selben Zug von der
vmroles-Claim-Prüfung auf `DbAuthorizationService` umgestellt.
