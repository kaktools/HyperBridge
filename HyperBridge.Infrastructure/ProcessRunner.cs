using System.Diagnostics;
using System.Text;
using HyperBridge.Core.Contracts;
using HyperBridge.Core.Models;

namespace HyperBridge.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(ProcessExecutionOptions options, CancellationToken cancellationToken)
    {
        using var process = new Process();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = options.FileName,
            Arguments = options.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                ? Environment.CurrentDirectory
                : options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            stdout.AppendLine(args.Data);
            options.OnStdOut?.Invoke(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            stderr.AppendLine(args.Data);
            options.OnStdErr?.Invoke(args.Data);
        };

        var started = process.Start();
        if (!started)
        {
            throw new InvalidOperationException($"Could not start process '{options.FileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(options.TimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            TryKillProcess(process);
            return new ProcessExecutionResult
            {
                ExitCode = -1,
                TimedOut = true,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
            };
        }

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            TimedOut = false,
        };
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Intentionally ignored because timeout handling should not throw secondary errors.
        }
    }
}