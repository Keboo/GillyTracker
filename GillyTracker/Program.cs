using GillyTracker.Core;
using GillyTracker.Core.Auth;
using GillyTracker.Data;
using GillyTracker.Middleware;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
    .AddDatabase();

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

var petTrackerAdminsGroupObjectId = builder.Configuration["Authorization:PetTrackerAdminsGroupObjectId"]?.Trim();

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

var microsoftTenantId = builder.Configuration["Authentication:Microsoft:TenantId"]?.Trim();
var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"]?.Trim();
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]?.Trim();
var microsoftCallbackPath = builder.Configuration["Authentication:Microsoft:CallbackPath"]?.Trim();

if (!string.IsNullOrWhiteSpace(microsoftTenantId) &&
    !string.IsNullOrWhiteSpace(microsoftClientId) &&
    !string.IsNullOrWhiteSpace(microsoftClientSecret))
{
    if (string.IsNullOrWhiteSpace(microsoftCallbackPath))
    {
        microsoftCallbackPath = "/signin-microsoft";
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
        options.ClientSecret = microsoftClientSecret;
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
    var spaIndexFilePath = Path.Combine(app.Environment.WebRootPath, "index.html");
    var spaRoutes = new[]
    {
        "/",
        "/login",
        "/admin/sightings"
    };

    foreach (var route in spaRoutes)
    {
        app.MapGet(route, () => Results.File(spaIndexFilePath, "text/html"));
    }

    app.MapFallback(() => Results.LocalRedirect("/"));
}

app.Run();

// Simple no-op email sender
internal class NoOpEmailSender<TUser> : IEmailSender<TUser> where TUser : class
{
    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) => Task.CompletedTask;
    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) => Task.CompletedTask;
    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) => Task.CompletedTask;
}
