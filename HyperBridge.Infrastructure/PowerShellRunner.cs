using HyperBridge.Core.Contracts;
using HyperBridge.Core.Models;

namespace HyperBridge.Infrastructure;

public sealed class PowerShellRunner(IProcessRunner processRunner) : IPowerShellRunner
{
    public async Task<PowerShellExecutionResult> RunAsync(string script, int timeoutMs, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        var escaped = script.Replace("\"", "`\"");
        var options = new ProcessExecutionOptions
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{escaped}\"",
            TimeoutMs = timeoutMs,
            OnStdOut = onOutput,
            OnStdErr = onOutput,
        };

        var result = await processRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        return new PowerShellExecutionResult
        {
            Success = result.ExitCode == 0 && !result.TimedOut,
            ExitCode = result.ExitCode,
            Output = result.StandardOutput,
            Error = result.StandardError,
        };
    }
}