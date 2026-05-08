# Release Notes

## 1.1.3 - 2026-05-08

Wartungs- und UX-Release mit Fokus auf bessere Standardwerte in der Zielkonfiguration und weniger unnötige Blocker im Ablauf.

### Neu

- Automatische Übernahme von VM-Ressourcen:
	- Beim Auswählen einer VirtualBox-VM werden CPU-Anzahl und RAM als Startwerte in die Hyper-V-Zielkonfiguration übernommen.
	- Alle übernommenen Werte bleiben im Wizard weiterhin frei anpassbar.
- RAM-Eingabe in GB:
	- RAM-Felder im Wizard wurden von MB auf GB umgestellt (`RAM`, `Start-RAM`, `Min-RAM`, `Max-RAM`).
	- Interne Verarbeitung bleibt technisch in MB, die UI arbeitet benutzerfreundlich in GB.

### Verbessert

- Konsistente RAM-Normalisierung:
	- RAM-Werte aus VirtualBox werden bei der Übernahme auf volle GB nach unten normalisiert.
	- Beispiel: `8318 MB` wird zu `8 GB` (statt auf `9 GB` aufzurunden).

### Geändert

- Gast-Checkliste ist nicht mehr harter Blocker:
	- Eine unvollständige Checkliste wird im Pre-Check jetzt als Warnung statt als Fehler bewertet.
	- Die Migration kann damit gestartet werden, offene Punkte bleiben aber klar sichtbar dokumentiert.

## 1.1.2 - 2026-04-22

Funktionsrelease mit Fokus auf vollständige Migration von VirtualBox-VMs mit mehreren Datenträgern.

### Neu

- Multi-Disk-Unterstützung für die Migration:
	- HyperBridge erkennt nun alle migrierbaren Festplatten einer VirtualBox-VM statt nur der primären Disk.
	- Alle erkannten Datenträger werden in die Migrationspipeline übernommen.
- Hyper-V-Einbindung für Zusatzdatenträger:
	- Die Ziel-VM wird mit der primären VHDX erstellt.
	- Weitere konvertierte VHDX-Dateien werden danach automatisch per `Add-VMHardDiskDrive` angehängt.

### Verbessert

- Analysemodell erweitert:
	- Datenträgerpfade werden als Liste geführt.
	- Größenberechnung und Platzbedarf berücksichtigen jetzt die Summe aller Quell-Datenträger.
- Wizard-Oberfläche erweitert:
	- In der VM-Liste wird die Anzahl erkannter Disks angezeigt.
	- Im Analyse-Schritt wird die Datenträgeranzahl explizit ausgewiesen.
- Ergebnis- und Artefaktverwaltung verbessert:
	- Migrationsresultate enthalten Listen aller erzeugten VHD- und VHDX-Dateien.
	- Abschlussanzeige und Reports listen mehrere Quell-/Zieldatenträger auf.
	- Aufräumdialog kann mehrere temporäre VHD-Dateien in einem Schritt löschen.

### Behoben

- Problem behoben, bei dem bei VMs mit zwei oder mehr Festplatten nur die erste Disk migriert und in Hyper-V eingebunden wurde.

## 1.1.1 - 2026-04-10

Wartungs- und Stabilitätsrelease mit Fokus auf Bedienlogik im Wizard, konsistente Versionsanzeige und robustere VirtualBox-Klonläufe.

### Verbessert

- Wizard-Navigation im Footer überarbeitet:
	- Der Primärbutton zeigt im letzten Schritt jetzt korrekt `Migration starten` statt `Weiter`.
	- Nach Abschluss (Ergebnis-Schritt) wird der Primärbutton ausgeblendet.
	- Die Primäraktion wird kontextabhängig korrekt zwischen `Weiter` und `Migration starten` umgeschaltet.
- Update-Check vereinheitlicht:
	- Versionsanzeige im UI, in Logs und Dialogen wird nun konsistent als `Major.Minor.Patch` formatiert.

### Behoben

- VirtualBox-Klonfehler bei bereits registriertem Zielmedium (`already exists`) abgefangen:
	- Zielordner wird vor dem Klonlauf sichergestellt.
	- Vorhandene Ziel-Registrierung wird per `closemedium ... --delete` bereinigt.
	- Bei erkanntem Registrierungsfehler erfolgt ein automatischer einmaliger Wiederholungsversuch.
	- Vorhandene Zieldatei wird vor dem erneuten Lauf nach Möglichkeit entfernt.

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
