using HyperBridge.Core.Contracts;
using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public sealed class MigrationService(
    IVirtualBoxService virtualBoxService,
    IHyperVService hyperVService,
    ISystemCheckService systemCheckService,
    ILoggingService loggingService) : IMigrationService
{
    public async Task<PreCheckResult> RunPreChecksAsync(MigrationRequest request, CancellationToken cancellationToken)
    {
        var result = new PreCheckResult();
        var sourceDiskPaths = GetSourceDiskPaths(request.Analysis);

        if (request.Analysis.IsRunning)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "VM läuft noch",
                TechnicalDetail = "VMState ist auf running gesetzt.",
                PossibleCause = "Die Quelle wurde nicht sauber heruntergefahren.",
                NextStep = "Fahre die VM in VirtualBox vollständig herunter und starte den Check erneut.",
            });
        }

        if (request.Analysis.HasSavedState)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Warning,
                Title = "Saved State erkannt",
                TechnicalDetail = "VMState ist saved.",
                PossibleCause = "Gespeicherter Zustand kann inkonsistente Geräteinformationen enthalten.",
                NextStep = "VM einmal normal starten und sauber herunterfahren.",
            });
        }

        if (request.Analysis.HasSnapshots)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Warning,
                Title = "Snapshots vorhanden",
                TechnicalDetail = "Mindestens ein Snapshot wurde erkannt.",
                PossibleCause = "Snapshot-Ketten erhöhen das Migrationsrisiko.",
                NextStep = "Prüfe, ob du die VM konsolidieren möchtest, bevor du migrierst.",
            });
        }

        if (!request.GuestChecklist.IsComplete)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Warning,
                Title = "Gast-Checkliste unvollständig",
                TechnicalDetail = "Mindestens ein empfohlener Punkt in der Vorbereitung wurde nicht bestätigt.",
                PossibleCause = "Die Vorbereitung im Gast-OS ist noch nicht abgeschlossen.",
                NextStep = "Prüfe die offenen Punkte und dokumentiere bewusst verbleibende Risiken.",
            });
        }

        var folderCheck = await systemCheckService
            .CheckTargetFolderAsync(request.Target.TargetPath, request.Analysis.EstimatedRequiredBytes, cancellationToken)
            .ConfigureAwait(false);
        result.Issues.AddRange(folderCheck.Issues);

        if (sourceDiskPaths.Count == 0)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Kein Quelldatenträger erkannt",
                TechnicalDetail = "In der Analyse wurden keine migrierbaren Datenträger gefunden.",
                PossibleCause = "Die VM ist falsch konfiguriert oder nutzt ein nicht unterstütztes Format.",
                NextStep = "Prüfe die Datenträger in VirtualBox und starte die Analyse erneut.",
            });
        }

        foreach (var sourceDiskPath in sourceDiskPaths.Where(path => !File.Exists(path)))
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Quelldatei nicht gefunden",
                TechnicalDetail = sourceDiskPath,
                PossibleCause = "Der Datenträger wurde verschoben oder gelöscht.",
                NextStep = "Prüfe den Datenträgerpfad in VirtualBox und starte die Analyse neu.",
            });
        }

        if (!await hyperVService.AreCmdletsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Hyper-V PowerShell nicht verfügbar",
                TechnicalDetail = "Cmdlet New-VM wurde nicht gefunden.",
                PossibleCause = "Hyper-V ist nicht installiert oder das Modul ist nicht geladen.",
                NextStep = "Aktiviere Hyper-V und starte das Tool anschließend neu.",
            });
        }

        return result;
    }

    public async Task<MigrationResult> ExecuteMigrationAsync(MigrationRequest request, IProgress<MigrationProgressUpdate> progress, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var issues = new List<CheckIssue>();
        var sourceDiskPaths = GetSourceDiskPaths(request.Analysis);

        try
        {
            progress.Report(Update("Vorprüfung", "Prüfe Vorbedingungen", 5));
            var preCheck = await RunPreChecksAsync(request, cancellationToken).ConfigureAwait(false);
            issues.AddRange(preCheck.Issues);

            if (!preCheck.CanProceed)
            {
                var failedResult = new MigrationResult
                {
                    Status = MigrationStatus.Failed,
                    Summary = "Vorprüfung fehlgeschlagen.",
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = DateTime.UtcNow,
                };
                failedResult.Issues.AddRange(issues);
                return failedResult;
            }

            if (request.Target.DryRun)
            {
                progress.Report(Update("Dry Run", "Dry Run aktiv: keine Änderungen am System.", 100));
                var dryRunResult = new MigrationResult
                {
                    Status = MigrationStatus.DryRun,
                    Summary = "Dry Run erfolgreich abgeschlossen. Keine Änderungen durchgeführt.",
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = DateTime.UtcNow,
                    HyperVVmName = request.Target.HyperVVmName,
                };
                dryRunResult.Issues.AddRange(issues);
                return dryRunResult;
            }

            var baseFileName = SanitizeFileName(request.Target.HyperVVmName);
            var convertedDisks = new List<(string VhdPath, string VhdxPath)>();

            for (var index = 0; index < sourceDiskPaths.Count; index++)
            {
                var sourceDiskPath = sourceDiskPaths[index];
                var fileSuffix = index == 0 ? string.Empty : $"-disk{index + 1}";
                var vhdPath = Path.Combine(request.Target.TargetPath, $"{baseFileName}{fileSuffix}.vhd");
                var vhdxPath = Path.Combine(request.Target.TargetPath, $"{baseFileName}{fileSuffix}.vhdx");

                progress.Report(Update("Disk-Klon", $"Klonen der VirtualBox-Disk {index + 1}/{sourceDiskPaths.Count} nach VHD", 15));
                var cloneResult = await virtualBoxService
                    .CloneMediumToVhdAsync(sourceDiskPath, vhdPath, line => loggingService.LogInfo(line), cancellationToken)
                    .ConfigureAwait(false);

                if (cloneResult.ExitCode != 0)
                {
                    issues.Add(new CheckIssue
                    {
                        Severity = CheckSeverity.Error,
                        Title = "Klonen fehlgeschlagen",
                        TechnicalDetail = cloneResult.StandardError,
                        PossibleCause = "Dateizugriff blockiert oder VBoxManage-Fehler.",
                        NextStep = "Prüfe VirtualBox-Logs und ob die Quelldisk gesperrt ist.",
                    });
                    return Failed($"Disk-Klon fehlgeschlagen (Disk {index + 1}).");
                }

                progress.Report(Update("Konvertierung", $"Konvertiere VHD {index + 1}/{sourceDiskPaths.Count} nach VHDX", 35));
                var convertResult = await hyperVService
                    .ConvertVhdToVhdxAsync(vhdPath, vhdxPath, line => loggingService.LogInfo(line), cancellationToken)
                    .ConfigureAwait(false);

                if (!convertResult.Success)
                {
                    issues.Add(new CheckIssue
                    {
                        Severity = CheckSeverity.Error,
                        Title = "VHDX-Konvertierung fehlgeschlagen",
                        TechnicalDetail = convertResult.Error,
                        PossibleCause = "VHD wird verwendet oder Zielspeicher reicht nicht.",
                        NextStep = "Prüfe, ob die VHD in einem anderen Prozess eingebunden ist und ob genügend Speicher verfügbar ist.",
                    });
                    return Failed($"VHDX-Konvertierung fehlgeschlagen (Disk {index + 1}).");
                }

                convertedDisks.Add((vhdPath, vhdxPath));
            }

            var primaryDisk = convertedDisks[0];
            progress.Report(Update("Hyper-V VM", "Erzeuge Hyper-V Ziel-VM", 55));
            var createVmResult = await hyperVService
                .CreateVmAsync(request.Target, primaryDisk.VhdxPath, line => loggingService.LogInfo(line), cancellationToken)
                .ConfigureAwait(false);

            if (!createVmResult.Success)
            {
                issues.Add(new CheckIssue
                {
                    Severity = CheckSeverity.Error,
                    Title = "VM-Erstellung fehlgeschlagen",
                    TechnicalDetail = createVmResult.Error,
                    PossibleCause = "Ungültige Konfiguration oder fehlender Zugriff auf Hyper-V.",
                    NextStep = "Prüfe Hyper-V Rechte, VM-Namen und Switch-Konfiguration.",
                });
                return Failed("Erstellung der Hyper-V VM fehlgeschlagen.");
            }

            if (convertedDisks.Count > 1)
            {
                for (var index = 1; index < convertedDisks.Count; index++)
                {
                    progress.Report(Update("Zusatzdisk", $"Binde zusätzliche Festplatte {index + 1}/{convertedDisks.Count} ein", 65));
                    var attachResult = await hyperVService
                        .AttachDiskAsync(request.Target.HyperVVmName, convertedDisks[index].VhdxPath, line => loggingService.LogInfo(line), cancellationToken)
                        .ConfigureAwait(false);

                    if (!attachResult.Success)
                    {
                        issues.Add(new CheckIssue
                        {
                            Severity = CheckSeverity.Error,
                            Title = "Einbinden zusätzlicher Festplatte fehlgeschlagen",
                            TechnicalDetail = attachResult.Error,
                            PossibleCause = "Hyper-V konnte die zusätzliche VHDX nicht einhängen.",
                            NextStep = "Prüfe Hyper-V Rechte und Datenträgerpfade; hänge die Disk ggf. manuell ein.",
                        });
                        return Failed($"Zusatzdisk konnte nicht eingebunden werden (Disk {index + 1}).");
                    }
                }
            }

            if (request.Target.StartAfterCreation)
            {
                progress.Report(Update("VM Start", "Starte Hyper-V VM", 75));
                var startResult = await hyperVService.StartVmAsync(request.Target.HyperVVmName, cancellationToken).ConfigureAwait(false);
                if (!startResult.Success)
                {
                    issues.Add(new CheckIssue
                    {
                        Severity = CheckSeverity.Warning,
                        Title = "Start fehlgeschlagen",
                        TechnicalDetail = startResult.Error,
                        PossibleCause = "Boot-Modus passt nicht oder Treiberproblem im Gast.",
                        NextStep = "Prüfe Generation (Gen1/Gen2), Secure-Boot-Einstellung und VM-Konsole.",
                    });
                }
            }

            if (request.Target.CreateInitialCheckpoint)
            {
                progress.Report(Update("Checkpoint", "Erzeuge initialen Checkpoint", 88));
                var checkpointResult = await hyperVService
                    .CreateCheckpointAsync(request.Target.HyperVVmName, "Initial Migration", cancellationToken)
                    .ConfigureAwait(false);
                if (!checkpointResult.Success)
                {
                    issues.Add(new CheckIssue
                    {
                        Severity = CheckSeverity.Warning,
                        Title = "Checkpoint fehlgeschlagen",
                        TechnicalDetail = checkpointResult.Error,
                        PossibleCause = "Checkpoint-Typ eingeschränkt oder Speicherproblem.",
                        NextStep = "Erzeuge den Checkpoint später manuell im Hyper-V-Manager.",
                    });
                }
            }

            progress.Report(Update("Abschluss", "Migration abgeschlossen", 100));

            var successResult = new MigrationResult
            {
                Status = issues.Any(i => i.Severity == CheckSeverity.Error) ? MigrationStatus.PartialSuccess : MigrationStatus.Success,
                Summary = issues.Any(i => i.Severity == CheckSeverity.Warning)
                    ? "Migration abgeschlossen, aber mit Warnungen."
                    : "Migration erfolgreich abgeschlossen.",
                StartedAtUtc = startedAt,
                FinishedAtUtc = DateTime.UtcNow,
                VhdPath = convertedDisks[0].VhdPath,
                VhdxPath = convertedDisks[0].VhdxPath,
                VhdPaths = convertedDisks.Select(disk => disk.VhdPath).ToList(),
                VhdxPaths = convertedDisks.Select(disk => disk.VhdxPath).ToList(),
                HyperVVmName = request.Target.HyperVVmName,
            };
            successResult.Issues.AddRange(issues);
            successResult.PostActions.Add("Prüfe Netzwerkkonfiguration in der migrierten VM.");
            successResult.PostActions.Add("Prüfe VirtualBox Guest Additions und ersetze ggf. durch Hyper-V Integration Services.");
            return successResult;

            MigrationResult Failed(string summary)
            {
                progress.Report(Update("Fehler", summary, 100, LogLevel.Error));
                return new MigrationResult
                {
                    Status = MigrationStatus.Failed,
                    Summary = summary,
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = DateTime.UtcNow,
                };
            }
        }
        catch (OperationCanceledException)
        {
            var cancelledResult = new MigrationResult
            {
                Status = MigrationStatus.Cancelled,
                Summary = "Migration wurde abgebrochen.",
                StartedAtUtc = startedAt,
                FinishedAtUtc = DateTime.UtcNow,
            };
            cancelledResult.Issues.AddRange(issues);
            return cancelledResult;
        }
        catch (Exception ex)
        {
            issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Unerwarteter Fehler",
                TechnicalDetail = ex.ToString(),
                PossibleCause = "Nicht abgefangene Ausnahme während des Migrationsprozesses.",
                NextStep = "Prüfe Log und Bericht, behebe Ursache und starte Migration erneut.",
            });

            var failedResult = new MigrationResult
            {
                Status = MigrationStatus.Failed,
                Summary = "Migration durch unerwarteten Fehler beendet.",
                StartedAtUtc = startedAt,
                FinishedAtUtc = DateTime.UtcNow,
            };
            failedResult.Issues.AddRange(issues);
            return failedResult;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "HyperBridgeVm" : value;
    }

    private static IReadOnlyList<string> GetSourceDiskPaths(VirtualMachineAnalysis analysis)
    {
        if (analysis.DiskPaths.Count > 0)
        {
            return analysis.DiskPaths;
        }

        return string.IsNullOrWhiteSpace(analysis.DiskPath)
            ? Array.Empty<string>()
            : [analysis.DiskPath];
    }

    private static MigrationProgressUpdate Update(string step, string message, int percent, LogLevel level = LogLevel.Info)
    {
        return new MigrationProgressUpdate
        {
            Step = step,
            Message = message,
            Percent = percent,
            Level = level,
        };
    }
}