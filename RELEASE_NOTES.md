# Release Notes

## 1.1.0 - 2026-04-10

Erstes offizielles Release von HyperBridge.

HyperBridge ist ein geführtes Windows-Tool zur Migration von VirtualBox-VMs nach Hyper-V. Fokus dieses Releases ist eine sichere, nachvollziehbare und praxisnahe Migration mit klarer Schrittführung, technischer Transparenz und sauberem Reporting.

### Was HyperBridge macht

- Analysiert VirtualBox-VMs inklusive Datenträgerpfad, VM-Zustand und grundlegender Kompatibilitätshinweise.
- Führt Anwender durch einen 7-Schritte-Wizard von der Auswahl bis zum Ergebnis.
- Klont den Quell-Datenträger nach VHD und konvertiert anschließend nach VHDX.
- Erstellt und konfiguriert eine Hyper-V-VM auf Basis der gewählten Zielparameter.
- Unterstützt optionale Post-Aktionen wie VM-Start und initialen Checkpoint.
- Erstellt automatisch technische Berichte für Dokumentation und Nachvollziehbarkeit.

### Kernfunktionen

- Geführter Migrations-Wizard mit Statusanzeige und Fortschrittsbalken.
- Kompatibilitätsbewertung mit Handlungsempfehlungen (inkl. Gen1/Gen2-Hinweisen).
- Pre-Check mit Prüfungen zu VM-Zustand, Speicherplatz und Zielordner-Berechtigungen.
- Dry-Run-Modus ohne produktive Änderungen.
- Live-Logging in der Oberfläche und persistente Logdatei.
- Berichtserstellung als HTML und TXT.
- Optionales Löschen der temporären VHD nach erfolgreicher Konvertierung.

### System- und Sicherheitsprüfungen

- Prüfung auf Administratorrechte.
- Prüfung auf VirtualBox und VBoxManage.
- Prüfung auf Hyper-V-Feature und verfügbare Hyper-V-PowerShell-Cmdlets.
- Sichtbare Warnmeldungen bei fehlenden Voraussetzungen.
- Sperre des Migrationsstarts, solange Pflichtvoraussetzungen nicht erfüllt sind.

### Benutzeroberfläche und Bedienung

- Header-Logo verlinkt direkt auf das GitHub-Repository.
- Titelbar bewusst reduziert auf kleines Logo plus "HyperBridge".
- Verbesserte Lesbarkeit der Statusleiste.
- Versionsanzeige unten rechts.
- Update-Check gegen GitHub-Releases (manuell per Button, zusätzlich still im Startablauf).

### Technischer Ablauf der Migration

1. VM-Auswahl aus VirtualBox.
2. Analyse der Quelle und Ermittlung relevanter Metadaten.
3. Gastvorbereitungs-Checkliste.
4. Zielkonfiguration für Hyper-V.
5. Kompatibilitätsbewertung und Pre-Check.
6. Ausführung:
	- VBoxManage `clonemedium` nach VHD
	- Hyper-V `Convert-VHD` nach VHDX
	- Erstellung und Konfiguration der Hyper-V-VM
7. Abschluss mit Ergebnis, Logs und Report.

### Artefakte und Ausgaben

- Logdateien unter `%LOCALAPPDATA%\\HyperBridge\\Logs`.
- Reports unter `%LOCALAPPDATA%\\HyperBridge\\Reports`.
- Technische Detailausgabe direkt in der App.

### Voraussetzungen

- Windows 10 oder Windows 11.
- .NET 8 Laufzeit/SDK.
- Installiertes VirtualBox (inkl. VBoxManage).
- Aktiviertes Hyper-V inklusive PowerShell-Cmdlets.
- Administratorrechte für produktive Migrationsschritte.

### Bekannte Grenzen

- Keine automatische Reparatur von Bootproblemen im Gastbetriebssystem.
- Keine Änderungen an Benutzerkonten oder Passwörtern im Gast.
- Keine automatische Nachkonfiguration im migrierten Gast-OS.
- Kompatibilitätsaussagen sind technisch fundierte Empfehlungen, keine absolute Garantie.

### Enthaltene Verbesserungen in diesem Release

- GitHub-Repository-Link direkt über das Header-Logo.
- Versionsanzeige in der Statusleiste unten rechts.
- Update-Check gegen GitHub-Releases inklusive manueller Prüfung per Button.
- Sichtbare Warnmeldungen bei fehlendem VirtualBox oder fehlendem Hyper-V.
- Statusleiste unten links besser lesbar, kein Abschneiden von Unterlängen wie bei "y".
- Titelbar vereinfacht auf kleines Logo plus "HyperBridge".
- Header-Logo linksbündig ausgerichtet.
- Migrationsstart ist jetzt klar gesperrt, wenn Voraussetzungen nicht erfüllt sind.
- Nach erfolgreicher Migration fragt HyperBridge, ob die temporäre `.vhd` gelöscht werden soll, wenn `.vhdx` vorhanden ist.
- Entscheidung (gelöscht oder beibehalten) wird in technische Details und Logs übernommen.
- Repository-Bereinigung von generierten Build-Artefakten (`bin`, `obj`, `dist`).
- `.gitignore` erweitert um `dist/`, `installer/` und `build.bat`.
