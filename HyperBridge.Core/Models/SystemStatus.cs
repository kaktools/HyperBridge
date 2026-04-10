namespace HyperBridge.Core.Models;

public sealed class SystemStatus
{
    public bool IsAdmin { get; init; }
    public bool IsVirtualBoxInstalled { get; init; }
    public bool IsHyperVAvailable { get; init; }
    public bool IsVBoxManageAvailable { get; init; }
    public bool IsHyperVPowerShellAvailable { get; init; }
    public string VBoxManagePath { get; init; } = string.Empty;
    public string VirtualBoxVersion { get; init; } = string.Empty;
}