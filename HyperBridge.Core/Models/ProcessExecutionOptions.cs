namespace HyperBridge.Core.Models;

public sealed class ProcessExecutionOptions
{
    public string FileName { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 180000;
    public Action<string>? OnStdOut { get; init; }
    public Action<string>? OnStdErr { get; init; }
}