# Release Notes

## 1.1.0 - 2026-04-10

### Neu

- GitHub-Repository-Link direkt über das Header-Logo.
- Versionsanzeige in der Statusleiste unten rechts.
- Update-Check gegen GitHub-Releases inklusive manueller Prüfung per Button.
- Sichtbare Warnmeldungen bei fehlendem VirtualBox oder fehlendem Hyper-V.

### Verbessert

- Statusleiste unten links besser lesbar, kein Abschneiden von Unterlängen wie bei "y".
- Titelbar vereinfacht auf kleines Logo plus "HyperBridge".
- Header-Logo linksbündig ausgerichtet.
- Migrationsstart ist jetzt klar gesperrt, wenn Voraussetzungen nicht erfüllt sind.

### Migration und Cleanup

- Nach erfolgreicher Migration fragt HyperBridge, ob die temporäre `.vhd` gelöscht werden soll, wenn `.vhdx` vorhanden ist.
- Entscheidung (gelöscht oder beibehalten) wird in technische Details und Logs übernommen.

### Wartung

- Repository-Bereinigung von generierten Build-Artefakten (`bin`, `obj`, `dist`).
- `.gitignore` erweitert um `dist/`, `installer/` und `build.bat`.
