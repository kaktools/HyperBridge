namespace HyperBridge.Core.Models;

public sealed class ReportData
{
    public string SourceVm { get; init; } = string.Empty;
    public string SourceDisk { get; init; } = string.Empty;
    public string TargetVm { get; init; } = string.Empty;
    public string TargetVhdx { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime FinishedAtUtc { get; init; }
    public string ConfigurationJson { get; init; } = string.Empty;
    public string ResultSummary { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LogEntry> LogExcerpt { get; init; } = Array.Empty<LogEntry>();
}