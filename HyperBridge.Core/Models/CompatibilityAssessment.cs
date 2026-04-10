using HyperBridge.Core.Enums;

namespace HyperBridge.Core.Models;

public sealed class CompatibilityAssessment
{
    public CompatibilityLevel Level { get; init; }
    public string Recommendation { get; init; } = string.Empty;
    public int SuggestedGeneration { get; init; } = 2;
    public List<string> Reasons { get; } = [];
    public List<string> Actions { get; } = [];
}