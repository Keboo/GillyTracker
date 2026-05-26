using System.Security.Claims;

using GillyTracker.Core.Auth;
using GillyTracker.Data;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GillyTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    AdminAccessSettings adminAccessSettings,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("microsoft/login")]
    public async Task<IActionResult> MicrosoftLogin([FromQuery] string? returnUrl = "/admin/sightings")
    {
        AuthenticationScheme? microsoftScheme =
            await authenticationSchemeProvider.GetSchemeAsync(AdminAccessSettings.MicrosoftAuthenticationScheme);

        if (microsoftScheme is null)
        {
            var missingSettings = new List<string>();
            if (string.IsNullOrWhiteSpace(GetConfigValue(
                "Authentication:Microsoft:TenantId",
                "Authentication__Microsoft__TenantId",
                "Authentication--Microsoft--TenantId")))
            {
                missingSettings.Add("TenantId");
            }

            if (string.IsNullOrWhiteSpace(GetConfigValue(
                "Authentication:Microsoft:ClientId",
                "Authentication__Microsoft__ClientId",
                "Authentication--Microsoft--ClientId")))
            {
                missingSettings.Add("ClientId");
            }

            if (string.IsNullOrWhiteSpace(GetConfigValue(
                "Authentication:Microsoft:ClientSecret",
                "Authentication__Microsoft__ClientSecret",
                "Authentication--Microsoft--ClientSecret")))
            {
                missingSettings.Add("ClientSecret");
            }

            string missingSettingsDetail = missingSettings.Count > 0
                ? $" Missing required settings: {string.Join(", ", missingSettings)}."
                : string.Empty;

            return Problem(
                detail: $"Microsoft Entra authentication is not configured on this environment.{missingSettingsDetail}",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        string callbackUrl = Url.Action(nameof(MicrosoftCallback), values: new { returnUrl = safeReturnUrl })
            ?? $"/api/auth/microsoft/callback?returnUrl={Uri.EscapeDataString(safeReturnUrl)}";

        return Challenge(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            AdminAccessSettings.MicrosoftAuthenticationScheme);
    }

    [HttpGet("microsoft/callback")]
    public IActionResult MicrosoftCallback([FromQuery] string? returnUrl = "/admin/sightings")
    {
        return LocalRedirect(GetSafeReturnUrl(returnUrl));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("user")]
    public IActionResult GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Ok(new UserInfo { IsAuthenticated = false });
        }

        return Ok(new UserInfo
        {
            UserId = userManager.GetUserId(User) ?? "",
            UserName = User.Identity?.Name ?? userManager.GetUserName(User) ?? "",
            Email = userManager.GetUserName(User) ?? User.FindFirstValue(ClaimTypes.Email) ?? "",
            IsAuthenticated = true,
            IsAdmin = AdminAuthorization.IsPetTrackerAdmin(User, adminAccessSettings.PetTrackerAdminsGroupObjectId)
        });
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        const string defaultReturnUrl = "/admin/sightings";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return defaultReturnUrl;
        }

        if (Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? absoluteReturnUrl))
        {
            bool isSameHost = string.Equals(
                absoluteReturnUrl.Host,
                HttpContext.Request.Host.Host,
                StringComparison.OrdinalIgnoreCase);

            if (isSameHost && Url.IsLocalUrl(absoluteReturnUrl.PathAndQuery))
            {
                return absoluteReturnUrl.PathAndQuery;
            }
        }

        return defaultReturnUrl;
    }

    private string? GetConfigValue(params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

public record UserInfo
{
    public string UserId { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool IsAuthenticated { get; init; }
    public bool IsAdmin { get; init; }
}
