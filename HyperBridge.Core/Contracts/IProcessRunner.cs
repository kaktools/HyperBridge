using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(ProcessExecutionOptions options, CancellationToken cancellationToken);
}