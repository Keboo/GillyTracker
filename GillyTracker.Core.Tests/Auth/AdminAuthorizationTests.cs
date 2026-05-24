using System.Security.Claims;

using GillyTracker.Core.Auth;

namespace GillyTracker.Core.Tests.Auth;

public class AdminAuthorizationTests
{
    [Test]
    public async Task ReturnsFalseWhenUserIsNotAuthenticated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "group-123");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ReturnsFalseWhenAdminGroupIdIsMissing()
    {
        var identity = new ClaimsIdentity([new Claim("groups", "group-123")], authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ReturnsTrueWhenMatchingGroupsClaimExists()
    {
        var identity = new ClaimsIdentity([new Claim("groups", "group-123")], authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "group-123");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ReturnsFalseWhenGroupsClaimDoesNotContainAdminGroup()
    {
        var identity = new ClaimsIdentity([new Claim("groups", "group-999")], authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "group-123");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ReturnsTrueWhenMatchingGroupsClaimExistsWithWsFederationClaimType()
    {
        var identity = new ClaimsIdentity(
            [new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", "group-123")],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "group-123");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ReturnsTrueWhenMatchingGroupsClaimExistsWithMicrosoftClaimType()
    {
        var identity = new ClaimsIdentity(
            [new Claim("http://schemas.microsoft.com/claims/groups", "group-123")],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = AdminAuthorization.IsPetTrackerAdmin(principal, "group-123");

        await Assert.That(result).IsTrue();
    }
}
