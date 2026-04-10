using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IPowerShellRunner
{
    Task<PowerShellExecutionResult> RunAsync(string script, int timeoutMs, Action<string>? onOutput, CancellationToken cancellationToken);
}