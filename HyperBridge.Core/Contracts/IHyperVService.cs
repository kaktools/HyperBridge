using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IHyperVService
{
    Task<bool> IsHyperVAvailableAsync(CancellationToken cancellationToken);
    Task<bool> AreCmdletsAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetVirtualSwitchesAsync(CancellationToken cancellationToken);
    Task<PowerShellExecutionResult> ConvertVhdToVhdxAsync(string sourceVhdPath, string targetVhdxPath, Action<string>? onOutput, CancellationToken cancellationToken);
    Task<PowerShellExecutionResult> CreateVmAsync(TargetConfiguration configuration, string vhdxPath, Action<string>? onOutput, CancellationToken cancellationToken);
    Task<PowerShellExecutionResult> StartVmAsync(string vmName, CancellationToken cancellationToken);
    Task<PowerShellExecutionResult> CreateCheckpointAsync(string vmName, string checkpointName, CancellationToken cancellationToken);
}