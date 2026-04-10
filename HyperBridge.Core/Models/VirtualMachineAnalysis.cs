namespace HyperBridge.Core.Models;

public sealed class VirtualMachineAnalysis
{
    public required VirtualMachineSummary Summary { get; init; }
    public string DiskPath { get; init; } = string.Empty;
    public string DiskType { get; init; } = "Unknown";
    public bool IsRunning { get; init; }
    public bool HasSavedState { get; init; }
    public bool HasSnapshots { get; init; }
    public long SourceDiskBytes { get; init; }
    public long EstimatedRequiredBytes { get; init; }
    public long AvailableTargetBytes { get; init; }
    public List<string> Notes { get; } = [];
}