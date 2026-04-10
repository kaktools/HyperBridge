using HyperBridge.Core.Enums;

namespace HyperBridge.Core.Models;

public sealed class MigrationResult
{
    public MigrationStatus Status { get; init; }
    public string VhdPath { get; init; } = string.Empty;
    public string VhdxPath { get; init; } = string.Empty;
    public string HyperVVmName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<string> PostActions { get; } = [];
    public List<CheckIssue> Issues { get; } = [];
    public DateTime StartedAtUtc { get; init; }
    public DateTime FinishedAtUtc { get; init; }
}