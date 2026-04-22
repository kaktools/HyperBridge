namespace HyperBridge.Core.Models;

public sealed class VirtualMachineSummary
{
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string State { get; init; } = "Unknown";
    public string GuestOsType { get; init; } = "Unknown";
    public int MemoryMb { get; init; }
    public int CpuCount { get; init; }
    public bool HasSnapshots { get; init; }
    public IReadOnlyList<string> DiskPaths { get; init; } = Array.Empty<string>();
    public string PrimaryDiskPath { get; init; } = string.Empty;
}