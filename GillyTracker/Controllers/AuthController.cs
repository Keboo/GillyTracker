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
    private readonly string[] _allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

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
        const string defaultReturnUrl = "/admin/sightings";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return GetDefaultReturnUrl(defaultReturnUrl);
        }

        if (Url.IsLocalUrl(returnUrl))
        {
            return ToFrontendUrlOrPath(returnUrl);
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? absoluteReturnUrl))
        {
            if (IsAllowedAbsoluteReturnUrl(absoluteReturnUrl))
            {
                return absoluteReturnUrl.AbsoluteUri;
            }
        }

        return GetDefaultReturnUrl(defaultReturnUrl);
    }

    private bool IsAllowedAbsoluteReturnUrl(Uri absoluteReturnUrl)
    {
        if (!(absoluteReturnUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            absoluteReturnUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!Url.IsLocalUrl(absoluteReturnUrl.PathAndQuery))
        {
            return false;
        }

        if (HttpContext.Request.Host.HasValue &&
            absoluteReturnUrl.Host.Equals(HttpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase) &&
            GetPort(absoluteReturnUrl) == GetPort(HttpContext.Request.Host, absoluteReturnUrl.Scheme))
        {
            return true;
        }

        foreach (string allowedOrigin in _allowedOrigins)
        {
            if (Uri.TryCreate(allowedOrigin, UriKind.Absolute, out Uri? allowedOriginUri) &&
                allowedOriginUri.Scheme.Equals(absoluteReturnUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
                allowedOriginUri.Host.Equals(absoluteReturnUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                GetPort(allowedOriginUri) == GetPort(absoluteReturnUrl))
            {
                return true;
            }
        }

        return false;
    }

    private string GetDefaultReturnUrl(string defaultReturnPath)
    {
        return ToFrontendUrlOrPath(defaultReturnPath);
    }

    private string ToFrontendUrlOrPath(string pathAndQuery)
    {
        if (TryGetPreferredFrontendOrigin(out string frontendOrigin))
        {
            return new Uri(new Uri(frontendOrigin), pathAndQuery).ToString();
        }

        return pathAndQuery;
    }

    private bool TryGetPreferredFrontendOrigin(out string origin)
    {
        foreach (string allowedOrigin in _allowedOrigins)
        {
            if (Uri.TryCreate(allowedOrigin, UriKind.Absolute, out Uri? allowedOriginUri))
            {
                origin = allowedOriginUri.GetLeftPart(UriPartial.Authority);
                return true;
            }
        }

        origin = string.Empty;
        return false;
    }

    private static int GetPort(Uri uri)
    {
        if (!uri.IsDefaultPort)
        {
            return uri.Port;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
    }

    private static int GetPort(HostString host, string scheme)
    {
        if (host.Port.HasValue)
        {
            return host.Port.Value;
        }

        return scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
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
