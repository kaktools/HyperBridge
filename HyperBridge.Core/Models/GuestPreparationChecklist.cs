namespace HyperBridge.Core.Models;

public sealed class GuestPreparationChecklist
{
    public bool HostnameChanged { get; set; }
    public bool LocalUsersReviewed { get; set; }
    public bool UserPasswordsChanged { get; set; }
    public bool AdminPasswordChanged { get; set; }
    public bool NetworkParametersDocumented { get; set; }
    public bool GuestAdditionsRemovedOrPlanned { get; set; }
    public bool GuestShutdownCleanly { get; set; }
    public string Notes { get; set; } = string.Empty;

    public bool IsComplete =>
        HostnameChanged
        && LocalUsersReviewed
        && UserPasswordsChanged
        && AdminPasswordChanged
        && NetworkParametersDocumented
        && GuestAdditionsRemovedOrPlanned
        && GuestShutdownCleanly;
}