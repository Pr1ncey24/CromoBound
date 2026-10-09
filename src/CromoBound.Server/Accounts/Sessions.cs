using System.Globalization;
using System.Security.Claims;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Accounts;

/// <summary>The session cookie's contents and checks (spec §4.3).</summary>
internal static class Sessions
{
    public const string StampClaim = "stamp";

    /// <summary>The user's id, name and security stamp, the player role, and the admin role for admins.</summary>
    public static ClaimsPrincipal PrincipalFor(UserEntity user)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(StampClaim, user.SecurityStamp),
            new(ClaimTypes.Role, Roles.Seat),
        ];
        if (user.IsAdmin) claims.Add(new(ClaimTypes.Role, Roles.Steward));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static int? UserId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>On every request: the session must still be current (<see cref="IsCurrentAsync"/>); otherwise it ends (a changed
    /// password, role or disabled flag takes effect at once).</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<CromoDbContext>();
        if (await IsCurrentAsync(context.Principal!, db)) return;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Whether the session's account still exists, is enabled and carries the session's security stamp. Also checked on
    /// every hub call, because a hub connection outlives the request that opened it.</summary>
    public static async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, CromoDbContext db)
    {
        var id = UserId(principal);
        var stamp = principal.FindFirstValue(StampClaim);
        var user = id is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        return user is { Disabled: false } && user.SecurityStamp == stamp;
    }

    /// <summary>Signed out: a page request goes to the login page, anything else gets a bare 401.</summary>
    public static Task ChallengeAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsPageRequest(context.Request)) context.Response.Redirect("/login");
        else context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>Signed in without the role: a bare 403, never a redirect and never a reason.</summary>
    public static Task ForbidAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool IsPageRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && !request.Path.StartsWithSegments("/api")
        && !request.Path.StartsWithSegments("/hub")
        && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
}
