using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IMigrationService
{
    Task<PreCheckResult> RunPreChecksAsync(MigrationRequest request, CancellationToken cancellationToken);
    Task<MigrationResult> ExecuteMigrationAsync(MigrationRequest request, IProgress<MigrationProgressUpdate> progress, CancellationToken cancellationToken);
}