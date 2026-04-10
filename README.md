# HyperBridge

Geführte Migration von VirtualBox nach Hyper-V auf Windows 10 und 11.

Version: 1.1.1

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
4. Gastvorbereitung bestätigen.
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
