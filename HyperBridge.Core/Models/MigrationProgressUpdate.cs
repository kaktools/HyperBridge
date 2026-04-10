using HyperBridge.Core.Enums;

namespace HyperBridge.Core.Models;

public sealed class MigrationProgressUpdate
{
    public string Step { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Percent { get; init; }
    public LogLevel Level { get; init; } = LogLevel.Info;
    public string TechnicalDetails { get; init; } = string.Empty;
}