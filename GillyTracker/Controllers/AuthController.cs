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
    AdminAccessSettings adminAccessSettings,
    IConfiguration configuration) : ControllerBase
{
    private readonly string? _frontendUrl = GetConfigValue("FrontendUrl", "Frontend__Url", "Frontend--Url");

    [HttpGet("microsoft/login")]
    public IActionResult MicrosoftLogin([FromQuery] string? returnUrl = "/admin/sightings")
    {
        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        string callbackUrl = Url.Action(nameof(MicrosoftCallback), values: new { returnUrl = safeReturnUrl })
            ?? $"/api/auth/microsoft/callback?returnUrl={Uri.EscapeDataString(safeReturnUrl)}";

        try
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = callbackUrl },
                AdminAccessSettings.MicrosoftAuthenticationScheme);
        }
        catch (InvalidOperationException ex)
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
            string exceptionDetail = string.IsNullOrWhiteSpace(ex.Message)
                ? ex.GetType().Name
                : $"{ex.GetType().Name}: {ex.Message}";

            return Problem(
                detail: $"Microsoft Entra authentication is not configured on this environment.{missingSettingsDetail} {exceptionDetail}",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("microsoft/callback")]
    public IActionResult MicrosoftCallback([FromQuery] string? returnUrl = "/admin/sightings")
    {
        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        if (Uri.TryCreate(safeReturnUrl, UriKind.Absolute, out Uri? absoluteReturnUrl))
        {
            return Redirect(absoluteReturnUrl.ToString());
        }

        return LocalRedirect(safeReturnUrl);
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
        const string defaultReturnPath = "/admin/sightings";
        string safeReturnPath = Url.IsLocalUrl(returnUrl) ? returnUrl : defaultReturnPath;
        return ToFrontendUrlOrPath(safeReturnPath);
    }

    private string ToFrontendUrlOrPath(string pathAndQuery)
    {
        if (TryGetFrontendOrigin(out string frontendOrigin))
        {
            return new Uri(new Uri(frontendOrigin), pathAndQuery).ToString();
        }

        return pathAndQuery;
    }

    private bool TryGetFrontendOrigin(out string origin)
    {
        if (Uri.TryCreate(_frontendUrl, UriKind.Absolute, out Uri? frontendUri) &&
            (frontendUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            frontendUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            origin = frontendUri.GetLeftPart(UriPartial.Authority);
            return true;
        }

        origin = string.Empty;
        return false;
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
