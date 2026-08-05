# Diagramme (PlantUML)

Alle Diagramme der Bachelorarbeit liegen als PlantUML-Quelltext in diesem Verzeichnis
(`*.puml`) und werden als PDF nach `rendered/` gerendert. Die PDFs werden im LaTeX-Dokument
per `\includegraphics` eingebunden (siehe `../bachelorarbeit.tex`).

## Voraussetzungen

- Java (OpenJDK 17+, getestet mit OpenJDK 21)
- `plantuml.jar`, abgelegt unter `../tools/plantuml.jar` (nicht versioniert, siehe unten)

PlantUML benötigt für Diagramme mit automatischem Layout (Klassen-, Komponenten-,
Use-Case- und Deploymentdiagramme) normalerweise Graphviz (`dot`). Steht kein Graphviz
zur Verfügung (z. B. ohne Root-Rechte), nutzen alle betroffenen `.puml`-Dateien in diesem
Projekt die eingebaute reine Java-Layout-Engine **Smetana** über
`!pragma layout smetana` am Dateianfang. Sequenzdiagramme benötigen ohnehin kein Graphviz.

## plantuml.jar besorgen

```bash
mkdir -p thesis/tools
curl -fL -o thesis/tools/plantuml.jar \
  "https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar"
```

## Neu rendern

Einzelnes Diagramm:

```bash
java -jar thesis/tools/plantuml.jar -tpdf thesis/diagrams/<name>.puml -o rendered
```

Alle Diagramme auf einmal (aus dem Verzeichnis `thesis/`):

```bash
java -jar tools/plantuml.jar -tpdf diagrams/*.puml -o rendered
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
| `er_diagramm_autorisierung.puml` | **Soll-Konzept**, noch nicht implementiert: geplantes DB-Autorisierungsmodell | 5.3 Sicherheitskonzept |
| `sequenz_login.puml` | Ablauf Login/Authentifizierung inkl. Fehlerfall | 5.3 Sicherheitskonzept |
| `sequenz_vm_aktion_autorisierung.puml` | Ablauf einer VM-Aktion inkl. Autorisierungsprüfung | 5.3 Sicherheitskonzept |
| `deployment_umgebungen.puml` | Entwicklungs- und Zielumgebung (Siemens) | 8.2 Weg zur Produktionsreife |

## Hinweis zur Genauigkeit

Die Diagramme `architektur_uebersicht`, `use_case_rollen`, `klassendiagramm_core`,
`sequenz_login` und `sequenz_vm_aktion_autorisierung` bilden den tatsächlichen Code in
`VmPortal.Core`/`VmPortal.Api` ab (Stand siehe Git-Historie dieses Verzeichnisses).
Nur `er_diagramm_autorisierung.puml` zeigt ein **geplantes, nicht implementiertes**
Zielmodell und ist im Diagramm sowie in der Bildunterschrift entsprechend gekennzeichnet.
