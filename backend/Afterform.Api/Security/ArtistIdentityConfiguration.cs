using System.Threading.RateLimiting;
using Afterform.Api.Data;
using Afterform.Api.Models;
using Afterform.Api.Uploads;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Afterform.Api.Security;

public static class ArtistIdentityConfiguration
{
    public const string LoginRatePolicy = "artist-login";

    public static void AddArtistIdentity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityCore<ArtistUser>(options =>
        {
            options.Password.RequiredLength = 15;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AfterformDbContext>()
        .AddSignInManager();

        builder.Services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = 220_000;
        });
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(5));
        builder.Services.AddSingleton<LoginTimingGuard>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        }).AddIdentityCookies();
        builder.Services.AddAuthorization();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = builder.Environment.IsDevelopment()
                ? "afterform.artist" : "__Host-afterform.artist";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/artist/login/";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else
                    context.Response.Redirect("/artist/login/");
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-AF-CSRF";
            options.Cookie.Name = builder.Environment.IsDevelopment()
                ? "afterform.csrf" : "__Host-afterform.csrf";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });

        var configuredKeyPath = builder.Configuration["DataProtection:KeysPath"];
        if (builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(configuredKeyPath))
        {
            var keyPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredKeyPath)
                ? Path.Combine(builder.Environment.ContentRootPath, "../../.local/data-protection")
                : Path.IsPathFullyQualified(configuredKeyPath) ? configuredKeyPath
                    : Path.Combine(builder.Environment.ContentRootPath, configuredKeyPath));
            var protection = builder.Services.AddDataProtection()
                .SetApplicationName("Afterform.ArtistPortal")
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        }

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(DemoUploadRules.UploadRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unauthenticated",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 6,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                    ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)) : 60;
                context.HttpContext.Response.Headers.RetryAfter = retry.ToString();
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests. Please try again shortly."
                        }
                    });
            };
        });
    }

    public static void UseArtistPageAccess(this WebApplication app)
    {
        // This runs before static files, so direct HTML URLs are protected too.
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var rawPath = path.Value ?? "/";
            // Windows file providers can interpret separators/aliases differently
            // from URL routing. Reject those paths before serving any static file.
            if (rawPath.Contains('\\') || rawPath.Contains("//", StringComparison.Ordinal)
                || rawPath.Contains(':')
                || rawPath.Split('/').Any(segment => segment.EndsWith('.') || segment.EndsWith(' ')))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (path.StartsWithSegments("/artist", StringComparison.OrdinalIgnoreCase))
            {
                var isPublic = path.Equals(new PathString("/artist/auth.js"), StringComparison.OrdinalIgnoreCase)
                    || path.Equals(new PathString("/artist/login"), StringComparison.OrdinalIgnoreCase)
                    || path.Equals(new PathString("/artist/login/"), StringComparison.OrdinalIgnoreCase)
                    || path.Equals(new PathString("/artist/login/index.html"), StringComparison.OrdinalIgnoreCase);
                context.Response.Headers.CacheControl = "no-store";
                if (!isPublic && context.User.Identity?.IsAuthenticated != true)
                {
                    await context.ChallengeAsync(IdentityConstants.ApplicationScheme);
                    return;
                }
            }
            await next(context);
        });
    }
}
