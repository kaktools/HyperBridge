using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface ISystemCheckService
{
    Task<SystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken);
    Task<PreCheckResult> CheckTargetFolderAsync(string targetFolder, long requiredBytes, CancellationToken cancellationToken);
}