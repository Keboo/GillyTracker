using System.Security.Claims;

namespace GillyTracker.Core.Auth;

public static class AdminAuthorization
{
    private const string GroupsClaimType = "groups";
    private static readonly string[] SupportedGroupsClaimTypes =
    [
        GroupsClaimType,
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups",
        "http://schemas.microsoft.com/claims/groups"
    ];

    public static bool IsPetTrackerAdmin(ClaimsPrincipal? user, string? petTrackerAdminsGroupObjectId)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(petTrackerAdminsGroupObjectId))
        {
            return false;
        }

        return user.Claims.Any(claim =>
            IsSupportedGroupsClaimType(claim.Type) &&
            claim.Value.Equals(petTrackerAdminsGroupObjectId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedGroupsClaimType(string claimType)
    {
        return SupportedGroupsClaimTypes.Any(supportedClaimType =>
            supportedClaimType.Equals(claimType, StringComparison.OrdinalIgnoreCase));
    }
}
