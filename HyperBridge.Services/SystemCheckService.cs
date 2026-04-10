using System.Security.Principal;
using System.Runtime.Versioning;
using HyperBridge.Core.Contracts;
using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

[SupportedOSPlatform("windows")]
public sealed class SystemCheckService(
    IVirtualBoxService virtualBoxService,
    IHyperVService hyperVService) : ISystemCheckService
{
    public async Task<SystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken)
    {
        var status = new SystemStatus
        {
            IsAdmin = IsAdministrator(),
        };

        try
        {
            var path = await virtualBoxService.FindVBoxManagePathAsync(cancellationToken).ConfigureAwait(false);
            var version = await virtualBoxService.GetVersionAsync(cancellationToken).ConfigureAwait(false);
            status = new SystemStatus
            {
                IsAdmin = status.IsAdmin,
                VBoxManagePath = path,
                IsVBoxManageAvailable = true,
                IsVirtualBoxInstalled = true,
                VirtualBoxVersion = version,
            };
        }
        catch
        {
            status = new SystemStatus
            {
                IsAdmin = status.IsAdmin,
                IsVBoxManageAvailable = false,
                IsVirtualBoxInstalled = false,
                VirtualBoxVersion = "Nicht gefunden",
            };
        }

        status = new SystemStatus
        {
            IsAdmin = status.IsAdmin,
            IsVBoxManageAvailable = status.IsVBoxManageAvailable,
            IsVirtualBoxInstalled = status.IsVirtualBoxInstalled,
            VBoxManagePath = status.VBoxManagePath,
            VirtualBoxVersion = status.VirtualBoxVersion,
            IsHyperVAvailable = await hyperVService.IsHyperVAvailableAsync(cancellationToken).ConfigureAwait(false),
            IsHyperVPowerShellAvailable = await hyperVService.AreCmdletsAvailableAsync(cancellationToken).ConfigureAwait(false),
        };

        return status;
    }

    public Task<PreCheckResult> CheckTargetFolderAsync(string targetFolder, long requiredBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new PreCheckResult();

        try
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var testFile = Path.Combine(targetFolder, ".write_test.tmp");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Schreibrechte fehlen",
                TechnicalDetail = ex.Message,
                PossibleCause = "Der Zielordner ist geschützt oder liegt auf einem schreibgeschützten Medium.",
                NextStep = "Wähle einen anderen Zielordner oder starte HyperBridge mit Administratorrechten.",
            });
            return Task.FromResult(result);
        }

        var root = Path.GetPathRoot(Path.GetFullPath(targetFolder)) ?? "C:\\";
        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < requiredBytes)
        {
            result.Issues.Add(new CheckIssue
            {
                Severity = CheckSeverity.Error,
                Title = "Unzureichender Speicherplatz",
                TechnicalDetail = $"Benötigt: {requiredBytes / (1024 * 1024 * 1024)} GiB, verfügbar: {drive.AvailableFreeSpace / (1024 * 1024 * 1024)} GiB",
                PossibleCause = "Der Datenträger hat nicht genügend freien Speicher für Clone und VHDX-Konvertierung.",
                NextStep = "Schaffe freien Speicherplatz oder wähle einen größeren Zielordner.",
            });
        }

        return Task.FromResult(result);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}