using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IVirtualBoxService
{
    Task<string> FindVBoxManagePathAsync(CancellationToken cancellationToken);
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VirtualMachineSummary>> GetVirtualMachinesAsync(CancellationToken cancellationToken);
    Task<VirtualMachineAnalysis> AnalyzeVmAsync(string vmName, string targetPath, CancellationToken cancellationToken);
    Task<ProcessExecutionResult> CloneMediumToVhdAsync(string sourceDiskPath, string targetVhdPath, Action<string>? onOutput, CancellationToken cancellationToken);
}