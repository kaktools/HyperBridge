using HyperBridge.Core.Enums;

namespace HyperBridge.Core.Models;

public sealed class CheckIssue
{
    public CheckSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TechnicalDetail { get; init; } = string.Empty;
    public string PossibleCause { get; init; } = string.Empty;
    public string NextStep { get; init; } = string.Empty;
}