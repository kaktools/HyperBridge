namespace HyperBridge.Core.Models;

public sealed class TargetConfiguration
{
    public string HyperVVmName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public int Generation { get; set; } = 2;
    public int MemoryMb { get; set; } = 4096;
    public bool DynamicMemoryEnabled { get; set; } = true;
    public int StartupMemoryMb { get; set; } = 4096;
    public int MinimumMemoryMb { get; set; } = 2048;
    public int MaximumMemoryMb { get; set; } = 8192;
    public int CpuCount { get; set; } = 2;
    public string VirtualSwitch { get; set; } = string.Empty;
    public bool SecureBootEnabled { get; set; } = true;
    public bool StartAfterCreation { get; set; }
    public bool CreateInitialCheckpoint { get; set; }
    public bool DryRun { get; set; }
}