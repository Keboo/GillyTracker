namespace GillyTracker.Core.Auth;

public sealed record AdminAccessSettings(string PetTrackerAdminsGroupObjectId)
{
    public const string PolicyName = "PetTrackerAdmins";
    public const string MicrosoftAuthenticationScheme = "MicrosoftOpenIdConnect";
}
