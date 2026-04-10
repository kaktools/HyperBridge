namespace HyperBridge.Core.Models;

public sealed class PreCheckResult
{
    public bool CanProceed => Issues.All(i => i.Severity != Enums.CheckSeverity.Error);
    public List<CheckIssue> Issues { get; } = [];
}