using System.Text.RegularExpressions;
using HyperBridge.Core.Contracts;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public sealed class VirtualBoxService(IProcessRunner processRunner, ILoggingService loggingService) : IVirtualBoxService
{
    private static readonly Regex VmLineRegex = new("^\"(?<name>.+)\"\\s+\\{(?<id>[^}]+)\\}$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedDiskExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vdi",
        ".vmdk",
        ".vhd",
        ".vhdx",
        ".img",
        ".raw",
        ".qcow",
        ".qcow2",
    };

    public async Task<string> FindVBoxManagePathAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Oracle", "VirtualBox", "VBoxManage.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Oracle", "VirtualBox", "VBoxManage.exe"),
        };

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            candidates.Add(Path.Combine(segment, "VBoxManage.exe"));
        }

        var found = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(found))
        {
            throw new FileNotFoundException("VBoxManage.exe wurde nicht gefunden. Bitte VirtualBox installieren oder den Pfad prüfen.");
        }

        return found;
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var vboxManage = await FindVBoxManagePathAsync(cancellationToken).ConfigureAwait(false);
        var result = await processRunner.RunAsync(new ProcessExecutionOptions
        {
            FileName = vboxManage,
            Arguments = "--version",
            TimeoutMs = 30000,
        }, cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0
            ? result.StandardOutput.Trim()
            : "Unbekannt";
    }

    public async Task<IReadOnlyList<VirtualMachineSummary>> GetVirtualMachinesAsync(CancellationToken cancellationToken)
    {
        var vboxManage = await FindVBoxManagePathAsync(cancellationToken).ConfigureAwait(false);
        var listResult = await processRunner.RunAsync(new ProcessExecutionOptions
        {
            FileName = vboxManage,
            Arguments = "list vms",
            TimeoutMs = 60000,
        }, cancellationToken).ConfigureAwait(false);

        if (listResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"VirtualBox VMs konnten nicht gelesen werden: {listResult.StandardError}");
        }

        var summaries = new List<VirtualMachineSummary>();
        var lines = listResult.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = VmLineRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var details = await GetMachineReadableInfoAsync(vboxManage, name, cancellationToken).ConfigureAwait(false);

            summaries.Add(new VirtualMachineSummary
            {
                Name = name,
                Id = match.Groups["id"].Value,
                State = details.GetValueOrDefault("VMState", "unknown"),
                GuestOsType = details.GetValueOrDefault("ostype", "Unknown"),
                MemoryMb = ParseInt(details.GetValueOrDefault("memory", "0")),
                CpuCount = ParseInt(details.GetValueOrDefault("cpus", "1")),
                HasSnapshots = ParseInt(details.GetValueOrDefault("SnapshotNameMachineMapping1", "0")) > 0 || details.Keys.Any(k => k.StartsWith("SnapshotName", StringComparison.OrdinalIgnoreCase)),
                PrimaryDiskPath = ResolvePrimaryDisk(details),
            });
        }

        return summaries;
    }

    public async Task<VirtualMachineAnalysis> AnalyzeVmAsync(string vmName, string targetPath, CancellationToken cancellationToken)
    {
        var vboxManage = await FindVBoxManagePathAsync(cancellationToken).ConfigureAwait(false);
        var details = await GetMachineReadableInfoAsync(vboxManage, vmName, cancellationToken).ConfigureAwait(false);
        var diskPath = ResolvePrimaryDisk(details);

        if (string.IsNullOrWhiteSpace(diskPath) || !File.Exists(diskPath))
        {
            throw new FileNotFoundException("Die Hauptdatenträger-Datei der VM konnte nicht gefunden werden.", diskPath);
        }

        if (!IsSupportedDiskFile(diskPath))
        {
            throw new InvalidOperationException($"Ungültiger Quell-Datenträger erkannt: '{diskPath}'. Erwartet wird eine Disk-Datei (z.B. .vdi/.vmdk), nicht die VM-Konfigurationsdatei.");
        }

        var fileInfo = new FileInfo(diskPath);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? "C:\\");
        var diskType = Path.GetExtension(diskPath).Trim('.').ToUpperInvariant();

        var summary = new VirtualMachineSummary
        {
            Name = vmName,
            Id = details.GetValueOrDefault("UUID", string.Empty),
            State = details.GetValueOrDefault("VMState", "unknown"),
            GuestOsType = details.GetValueOrDefault("ostype", "Unknown"),
            MemoryMb = ParseInt(details.GetValueOrDefault("memory", "0")),
            CpuCount = ParseInt(details.GetValueOrDefault("cpus", "1")),
            HasSnapshots = details.Keys.Any(k => k.StartsWith("SnapshotName", StringComparison.OrdinalIgnoreCase)),
            PrimaryDiskPath = diskPath,
        };

        var analysis = new VirtualMachineAnalysis
        {
            Summary = summary,
            DiskPath = diskPath,
            DiskType = diskType,
            IsRunning = string.Equals(summary.State, "running", StringComparison.OrdinalIgnoreCase),
            HasSavedState = string.Equals(summary.State, "saved", StringComparison.OrdinalIgnoreCase),
            HasSnapshots = summary.HasSnapshots,
            SourceDiskBytes = fileInfo.Length,
            EstimatedRequiredBytes = (long)(fileInfo.Length * 1.7),
            AvailableTargetBytes = drive.AvailableFreeSpace,
        };

        analysis.Notes.Add($"Quellformat erkannt: {diskType}");
        analysis.Notes.Add($"Geschätzter Mindestplatzbedarf: {analysis.EstimatedRequiredBytes / (1024 * 1024 * 1024)} GiB");
        return analysis;
    }

    public async Task<ProcessExecutionResult> CloneMediumToVhdAsync(string sourceDiskPath, string targetVhdPath, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        var vboxManage = await FindVBoxManagePathAsync(cancellationToken).ConfigureAwait(false);
        loggingService.LogInfo($"Starte VBox clone von '{sourceDiskPath}' nach '{targetVhdPath}'.");

        var result = await processRunner.RunAsync(new ProcessExecutionOptions
        {
            FileName = vboxManage,
            Arguments = $"clonemedium disk \"{sourceDiskPath}\" \"{targetVhdPath}\" --format VHD",
            TimeoutMs = 3600000,
            OnStdOut = onOutput,
            OnStdErr = onOutput,
        }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            loggingService.LogInfo("VBox-Klon nach VHD erfolgreich.");
        }
        else
        {
            loggingService.LogError($"VBox-Klon fehlgeschlagen: {result.StandardError}");
        }

        return result;
    }

    private async Task<Dictionary<string, string>> GetMachineReadableInfoAsync(string vboxManagePath, string vmName, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(new ProcessExecutionOptions
        {
            FileName = vboxManagePath,
            Arguments = $"showvminfo \"{vmName}\" --machinereadable",
            TimeoutMs = 120000,
        }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"VM-Details konnten nicht gelesen werden: {result.StandardError}");
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            map[key] = value;
        }

        return map;
    }

    private static string ResolvePrimaryDisk(Dictionary<string, string> details)
    {
        var mediumKey = details.Keys
            .Where(k => k.StartsWith("SATA", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("IDE", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("SCSI", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("NVMe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .FirstOrDefault();

        if (mediumKey is not null && details.TryGetValue(mediumKey, out var path) && IsSupportedDiskFile(path))
        {
            return path;
        }

        // Fallback: scan all machine-readable values for a known disk file path.
        foreach (var value in details.Values)
        {
            if (IsSupportedDiskFile(value))
            {
                return value;
            }
        }

        // Do not return CfgFile (.vbox) as disk source.
        return string.Empty;
    }

    private static bool IsSupportedDiskFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (string.Equals(path, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && SupportedDiskExtensions.Contains(ext);
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }
}