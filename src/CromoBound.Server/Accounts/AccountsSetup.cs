using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Accounts;

internal static class AccountsSetup
{
    /// <summary>Data protection (the keys that encrypt the cookie), cookie sign-in, the two policies with the player policy as the
    /// fallback and as the default for a bare authorization requirement (spec §4.1), and the login rate limit and lockout
    /// (spec §4.4).</summary>
    public static IServiceCollection AddCromoBoundAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        var protection = services.AddDataProtection().SetApplicationName("CromoBound");
        var keys = configuration[$"{ServerOptions.Section}:{nameof(ServerOptions.KeysFolder)}"];
        if (!string.IsNullOrEmpty(keys)) protection.PersistKeysToFileSystem(new DirectoryInfo(keys));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
        {
            cookie.Cookie.Name = "__Host-session";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.SlidingExpiration = true;
            cookie.Events.OnValidatePrincipal = Sessions.ValidateAsync;
            cookie.Events.OnRedirectToLogin = Sessions.ChallengeAsync;
            cookie.Events.OnRedirectToAccessDenied = Sessions.ForbidAsync;
        });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<ServerOptions>>((cookie, options) => cookie.ExpireTimeSpan = TimeSpan.FromHours(options.Value.CookieHours));

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(Policies.Seat, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Seat));
            authorization.AddPolicy(Policies.Steward, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Steward));
            authorization.FallbackPolicy = authorization.GetPolicy(Policies.Seat);
            authorization.DefaultPolicy = authorization.GetPolicy(Policies.Seat)!;
        });

        services.AddSingleton<LoginLockout>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = http.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value.LoginRequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }
}
