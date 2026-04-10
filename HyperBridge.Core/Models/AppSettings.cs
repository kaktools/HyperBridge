namespace HyperBridge.Core.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string LastTargetPath { get; set; } = string.Empty;
    public string LastSelectedVmName { get; set; } = string.Empty;
    public string LastVirtualSwitch { get; set; } = string.Empty;
    public bool LastDryRun { get; set; }
}