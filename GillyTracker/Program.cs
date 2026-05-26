using GillyTracker.Core;
using GillyTracker.Core.Auth;
using GillyTracker.Data;
using GillyTracker.Middleware;

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
    .AddDatabase();

string? keyVaultUri = GetConfigValue(
    builder.Configuration,
    "KeyVault:VaultUri",
    "KeyVault__VaultUri",
    "KeyVault--VaultUri");
if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out Uri? parsedKeyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        parsedKeyVaultUri,
        new DefaultAzureCredential());
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for frontend in development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // In development, allow any localhost origin for Vite dev server
            policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // In production, restrict to specific origins from configuration
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["https://dogtracker.keboo.dev"];
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

string? petTrackerAdminsGroupObjectId = GetConfigValue(
    builder.Configuration,
    "Authorization:PetTrackerAdminsGroupObjectId",
    "Authorization__PetTrackerAdminsGroupObjectId",
    "Authorization--PetTrackerAdminsGroupObjectId");

builder.Services.AddSingleton(new AdminAccessSettings(petTrackerAdminsGroupObjectId ?? ""));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAccessSettings.PolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            AdminAuthorization.IsPetTrackerAdmin(context.User, petTrackerAdminsGroupObjectId));
    });
});

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

authBuilder.AddIdentityCookies(options =>
{
    options.ApplicationCookie?.Configure(cookieOptions =>
    {
        cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        if (builder.Environment.IsDevelopment())
        {
            // In development, Vite dev server is cross-origin so we need SameSite=None
            cookieOptions.Cookie.SameSite = SameSiteMode.None;
        }
        else
        {
            // In production, frontend and backend are same-site (same eTLD+1),
            // so Lax cookies are sent on cross-origin fetch requests.
            // SameSite=None would be blocked by iOS Safari's ITP.
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
        }

        cookieOptions.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        cookieOptions.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
});

string? microsoftTenantId = GetConfigValue(
    builder.Configuration,
    "Authentication:Microsoft:TenantId",
    "Authentication__Microsoft__TenantId",
    "Authentication--Microsoft--TenantId");
string? microsoftClientId = GetConfigValue(
    builder.Configuration,
    "Authentication:Microsoft:ClientId",
    "Authentication__Microsoft__ClientId",
    "Authentication--Microsoft--ClientId");
string? microsoftClientSecret = GetConfigValue(
    builder.Configuration,
    "Authentication:Microsoft:ClientSecret",
    "Authentication__Microsoft__ClientSecret",
    "Authentication--Microsoft--ClientSecret");
string? microsoftCallbackPath = GetConfigValue(
    builder.Configuration,
    "Authentication:Microsoft:CallbackPath",
    "Authentication__Microsoft__CallbackPath",
    "Authentication--Microsoft--CallbackPath");

if (!string.IsNullOrWhiteSpace(microsoftTenantId) &&
    !string.IsNullOrWhiteSpace(microsoftClientId))
{
    if (string.IsNullOrWhiteSpace(microsoftCallbackPath))
    {
        microsoftCallbackPath = "/api/auth/microsoft/callback";
    }
    else if (!microsoftCallbackPath.StartsWith('/'))
    {
        microsoftCallbackPath = $"/{microsoftCallbackPath}";
    }

    authBuilder.AddOpenIdConnect(AdminAccessSettings.MicrosoftAuthenticationScheme, options =>
    {
        options.SignInScheme = IdentityConstants.ApplicationScheme;
        options.Authority = $"https://login.microsoftonline.com/{microsoftTenantId}/v2.0";
        options.ClientId = microsoftClientId;
        if (!string.IsNullOrWhiteSpace(microsoftClientSecret))
        {
            options.ClientSecret = microsoftClientSecret;
        }
        options.CallbackPath = microsoftCallbackPath;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name"
        };

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (!builder.Environment.IsDevelopment() &&
                    Uri.TryCreate(context.ProtocolMessage.RedirectUri, UriKind.Absolute, out Uri? redirectUri) &&
                    redirectUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    var httpsRedirectUri = new UriBuilder(redirectUri)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = -1
                    };
                    context.ProtocolMessage.RedirectUri = httpsRedirectUri.Uri.ToString();
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                if (!AdminAuthorization.IsPetTrackerAdmin(context.Principal, petTrackerAdminsGroupObjectId))
                {
                    context.Fail("Your account is not in the PetTrackerAdmins group.");
                }

                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("/login?error=auth_failed");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });
}

// No-op email sender for now (can be replaced with real implementation)
builder.Services.AddScoped<IEmailSender<ApplicationUser>>(sp => 
    new NoOpEmailSender<ApplicationUser>());

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseForwardedHeaders();

// Enable CORS
app.UseCors("AllowFrontend");

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Add exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Serve static files from React build (production only)
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA route handling for production
if (!app.Environment.IsDevelopment())
{
    string? webRootPath = app.Environment.WebRootPath;
    string? spaIndexFilePath = string.IsNullOrWhiteSpace(webRootPath)
        ? null
        : Path.Combine(webRootPath, "index.html");
    var spaRoutes = new[]
    {
        "/",
        "/login",
        "/admin/sightings"
    };

    if (!string.IsNullOrWhiteSpace(spaIndexFilePath) && File.Exists(spaIndexFilePath))
    {
        foreach (var route in spaRoutes)
        {
            app.MapGet(route, () => Results.File(spaIndexFilePath, "text/html"));
        }

        app.MapFallback(() => Results.LocalRedirect("/"));
    }
}

app.Run();

static string? GetConfigValue(IConfiguration configuration, params string[] keys)
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

// Simple no-op email sender
internal class NoOpEmailSender<TUser> : IEmailSender<TUser> where TUser : class
{
    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) => Task.CompletedTask;
    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) => Task.CompletedTask;
    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) => Task.CompletedTask;
}
