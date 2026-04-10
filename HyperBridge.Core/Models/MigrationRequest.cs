namespace HyperBridge.Core.Models;

public sealed class MigrationRequest
{
    public required VirtualMachineAnalysis Analysis { get; init; }
    public required TargetConfiguration Target { get; init; }
    public required GuestPreparationChecklist GuestChecklist { get; init; }
    public required CompatibilityAssessment Assessment { get; init; }
}