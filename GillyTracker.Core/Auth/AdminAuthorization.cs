using System.Security.Claims;

namespace GillyTracker.Core.Auth;

public static class AdminAuthorization
{
    private const string GroupsClaimType = "groups";

    public static bool IsPetTrackerAdmin(ClaimsPrincipal? user, string? petTrackerAdminsGroupObjectId)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(petTrackerAdminsGroupObjectId))
        {
            return false;
        }

        return user.Claims.Any(claim =>
            claim.Type.Equals(GroupsClaimType, StringComparison.OrdinalIgnoreCase) &&
            claim.Value.Equals(petTrackerAdminsGroupObjectId, StringComparison.OrdinalIgnoreCase));
    }
}
