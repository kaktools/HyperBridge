# HyperBridge

Geführte Migration von VirtualBox nach Hyper-V auf Windows 10 und 11.

Version: 1.1.3

## Neu in 1.1.3

- Zielkonfiguration übernimmt CPU und RAM der ausgewählten VirtualBox-VM jetzt automatisch als Startwerte, bleibt aber vollständig editierbar.
- RAM-Einstellungen im Wizard werden in GB statt MB angezeigt und eingegeben.
- RAM-Übernahme wird auf volle GB nach unten normalisiert (z. B. 8318 MB -> 8 GB), damit keine unerwartete Aufrundung entsteht.
- Gast-Checkliste bleibt wichtig, blockiert den Migrationsstart jedoch nicht mehr zwingend; offene Punkte werden als Warnung geführt.

## Neu in 1.1.2

- Mehrfach-Datenträger-Migration umgesetzt: HyperBridge erkennt jetzt alle an einer VirtualBox-VM angebundenen Festplatten und migriert nicht mehr nur die erste.
- Vollständiger Multi-Disk-Flow: Jede erkannte Quell-Disk wird nach VHD geklont, nach VHDX konvertiert und in Hyper-V eingebunden.
- Hyper-V-Provisionierung erweitert: Die VM wird mit der primären VHDX erstellt, zusätzliche VHDX-Dateien werden anschließend automatisch als weitere Festplatten angehängt.
- Analyse und UI verbessert: Im Wizard ist die Anzahl erkannter Datenträger sichtbar, inklusive konsolidierter Größenbewertung über alle Disks.
- Ergebnis, Report und Aufräumen aktualisiert: Abschlussdetails und Reports berücksichtigen jetzt mehrere Ziel-VHDX-Dateien; temporäre VHD-Dateien können gesammelt gelöscht werden.

## Neu in 1.1.1

- Wizard-Footer verbessert: Im letzten Schritt heißt der Hauptbutton jetzt korrekt "Migration starten" und wird nach dem Abschluss ausgeblendet.
- Update-Check zeigt Versionen jetzt konsistent im Format `Major.Minor.Patch` an (z. B. `1.1.1` statt uneinheitlicher Darstellungen).
- VirtualBox-Klon robuster gemacht: Zielverzeichnis wird vorab erstellt, bereits registrierte Ziel-Medien werden bereinigt und der Klon wird bei typischem Registrierungsfehler einmal automatisch neu versucht.

## Überblick

HyperBridge ist ein WPF-Tool mit klarer Schrittführung, technischer Vorprüfung und sauberem Ergebnisbericht. Das Ziel ist eine sichere und nachvollziehbare Migration einer bestehenden VirtualBox-VM in eine Hyper-V-VM.

## Highlights

- 7-Schritte-Wizard für den gesamten Migrationsablauf
- Kompatibilitätsbewertung mit klaren Handlungsempfehlungen
- Vorbedingungen-Prüfung (Admin, VirtualBox, Hyper-V)
- Live-Logging und Berichtsexport (HTML und TXT)
- Optionales Löschen der temporären VHD nach erfolgreicher Konvertierung
- Update-Check gegen GitHub-Releases

## Voraussetzungen

- Windows 10 oder Windows 11
- .NET SDK 8.0 oder neuer
- Installiertes VirtualBox inklusive VBoxManage
- Aktiviertes Hyper-V-Feature inklusive PowerShell-Cmdlets
- Für produktive Migration: Start als Administrator

## Schnellstart

```powershell
dotnet build HyperBridge.sln -c Debug
dotnet run --project HyperBridge.App/HyperBridge.App.csproj
```

## Migrationsablauf

1. Systemstatus prüfen.
2. VirtualBox-VM auswählen.
3. VM analysieren.
4. Gastvorbereitung prüfen und optional dokumentieren.
5. Zielkonfiguration festlegen.
6. Kompatibilität und Pre-Check ausführen.
7. Migration starten und Ergebnis prüfen.

## Projektstruktur

- HyperBridge.App: WPF-Oberfläche und ViewModels
- HyperBridge.Core: Modelle, Enums und Verträge
- HyperBridge.Services: Fachlogik für Analyse, Migration und Reports
- HyperBridge.Infrastructure: Runner und Settings

## Hinweise zur Bedienung

- Wenn VirtualBox oder Hyper-V fehlen, zeigt HyperBridge sichtbare Warnmeldungen und sperrt den Migrationsstart.
- Im Ergebnis-Schritt kann die nicht mehr benötigte VHD direkt per Ja/Nein-Abfrage gelöscht werden.
- Über den Button "Auf Updates prüfen" wird die installierte Version mit der neuesten GitHub-Release-Version verglichen.

## Logs und Reports

- Logs: `%LOCALAPPDATA%\HyperBridge\Logs`
- Reports: `%LOCALAPPDATA%\HyperBridge\Reports`

## Lizenz

Siehe Datei LICENSE.
