using HyperBridge.Core.Contracts;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public sealed class HyperVService(IPowerShellRunner powerShellRunner, ILoggingService loggingService) : IHyperVService
{
    public async Task<bool> IsHyperVAvailableAsync(CancellationToken cancellationToken)
    {
        const string script = "$f = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue; if ($f -and $f.State -eq 'Enabled') { 'true' } else { 'false' }";
        var result = await powerShellRunner.RunAsync(script, 30000, null, cancellationToken).ConfigureAwait(false);
        return result.Success && result.Output.Contains("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> AreCmdletsAvailableAsync(CancellationToken cancellationToken)
    {
        const string script = "if (Get-Command New-VM -ErrorAction SilentlyContinue) { 'true' } else { 'false' }";
        var result = await powerShellRunner.RunAsync(script, 30000, null, cancellationToken).ConfigureAwait(false);
        return result.Success && result.Output.Contains("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetVirtualSwitchesAsync(CancellationToken cancellationToken)
    {
        const string script = "Get-VMSwitch | Select-Object -ExpandProperty Name";
        var result = await powerShellRunner.RunAsync(script, 30000, null, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return Array.Empty<string>();
        }

        return result.Output
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<PowerShellExecutionResult> ConvertVhdToVhdxAsync(string sourceVhdPath, string targetVhdxPath, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        loggingService.LogInfo("Starte VHD nach VHDX-Konvertierung.");
        var script = $"Convert-VHD -Path '{EscapePath(sourceVhdPath)}' -DestinationPath '{EscapePath(targetVhdxPath)}' -VHDType Dynamic -ErrorAction Stop; Write-Output 'CONVERT_DONE'";
        return powerShellRunner.RunAsync(script, 3600000, onOutput, cancellationToken);
    }

    public Task<PowerShellExecutionResult> CreateVmAsync(TargetConfiguration configuration, string vhdxPath, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        var script = $@"
$vm = New-VM -Name '{Escape(configuration.HyperVVmName)}' -Generation {configuration.Generation} -MemoryStartupBytes {configuration.StartupMemoryMb}MB -Path '{EscapePath(configuration.TargetPath)}' -VHDPath '{EscapePath(vhdxPath)}' -ErrorAction Stop
Set-VMProcessor -VMName '{Escape(configuration.HyperVVmName)}' -Count {configuration.CpuCount} -ErrorAction Stop
if ({configuration.DynamicMemoryEnabled.ToString().ToLowerInvariant()}) {{
    Set-VMMemory -VMName '{Escape(configuration.HyperVVmName)}' -DynamicMemoryEnabled $true -StartupBytes {configuration.StartupMemoryMb}MB -MinimumBytes {configuration.MinimumMemoryMb}MB -MaximumBytes {configuration.MaximumMemoryMb}MB -ErrorAction Stop
}} else {{
    Set-VMMemory -VMName '{Escape(configuration.HyperVVmName)}' -DynamicMemoryEnabled $false -StartupBytes {configuration.MemoryMb}MB -ErrorAction Stop
}}
if ('{Escape(configuration.VirtualSwitch)}' -ne '') {{
    Connect-VMNetworkAdapter -VMName '{Escape(configuration.HyperVVmName)}' -SwitchName '{Escape(configuration.VirtualSwitch)}' -ErrorAction Stop
}}
if ({configuration.Generation} -eq 2) {{
    if ({configuration.SecureBootEnabled.ToString().ToLowerInvariant()}) {{
        Set-VMFirmware -VMName '{Escape(configuration.HyperVVmName)}' -EnableSecureBoot On -ErrorAction Stop
    }} else {{
        Set-VMFirmware -VMName '{Escape(configuration.HyperVVmName)}' -EnableSecureBoot Off -ErrorAction Stop
    }}
}}
Write-Output 'VM_CREATED'";

        return powerShellRunner.RunAsync(script, 240000, onOutput, cancellationToken);
    }

    public Task<PowerShellExecutionResult> AttachDiskAsync(string vmName, string vhdxPath, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        loggingService.LogInfo($"Binde zusätzliche Festplatte in Hyper-V ein: '{vhdxPath}'.");
        var script = $"Add-VMHardDiskDrive -VMName '{Escape(vmName)}' -Path '{EscapePath(vhdxPath)}' -ErrorAction Stop; Write-Output 'DISK_ATTACHED'";
        return powerShellRunner.RunAsync(script, 120000, onOutput, cancellationToken);
    }

    public Task<PowerShellExecutionResult> StartVmAsync(string vmName, CancellationToken cancellationToken)
    {
        var script = $"Start-VM -Name '{Escape(vmName)}' -ErrorAction Stop; Write-Output 'VM_STARTED'";
        return powerShellRunner.RunAsync(script, 120000, null, cancellationToken);
    }

    public Task<PowerShellExecutionResult> CreateCheckpointAsync(string vmName, string checkpointName, CancellationToken cancellationToken)
    {
        var script = $"Checkpoint-VM -Name '{Escape(vmName)}' -SnapshotName '{Escape(checkpointName)}' -ErrorAction Stop; Write-Output 'CHECKPOINT_CREATED'";
        return powerShellRunner.RunAsync(script, 120000, null, cancellationToken);
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapePath(string value) => Escape(Path.GetFullPath(value));
}