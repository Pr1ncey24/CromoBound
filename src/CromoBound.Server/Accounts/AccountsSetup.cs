using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Accounts;

internal static class AccountsSetup
{
    /// <summary>Data protection (the keys that encrypt the cookie), cookie sign-in, and the two policies with the player policy as
    /// the fallback, so everything without its own rule needs a signed-in player (spec §4.1).</summary>
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
        });
        return services;
    }
}
