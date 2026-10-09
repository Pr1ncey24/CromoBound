using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CromoBound.Server.Accounts;

/// <summary>Sign-in, sign-out, who am I, and the home page (spec §4.4, §5.2).</summary>
internal static class LoginEndpoints
{
    public const string Failure = "Invalid username or password.";
    public const string RateLimitPolicy = "login";

    public static void MapAccounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", () => Results.Content(Pages.Login(failed: false), "text/html")).AllowAnonymous();
        app.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
        app.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization(Policies.Seat);
        app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(user.Identity?.Name ?? "", user.IsInRole(Roles.Steward)))
            .RequireAuthorization(Policies.Seat);
        app.MapGet("/", (ClaimsPrincipal user) => Results.Content(Pages.Home(user.Identity?.Name ?? ""), "text/html"))
            .RequireAuthorization(Policies.Seat);
    }

    /// <summary>A form post (redirects home) or JSON (204). Every failure is the same answer, a locked name included; the time of a
    /// password check is spent even when the account doesn't exist or the name is locked.</summary>
    private static async Task<IResult> LoginAsync(HttpContext http, UserStore users, LoginLockout lockout)
    {
        var form = http.Request.HasFormContentType;
        var (userName, password) = await ReadCredentialsAsync(http.Request, form);
        if (userName is null || password is null) return Failed(form);
        if (UserStore.UserNameProblem(userName) is not null || lockout.IsLocked(userName))
        {
            users.VerifyNothing(password);
            return Failed(form);
        }
        var user = await users.FindAsync(userName);
        if (user is null) users.VerifyNothing(password);
        var valid = user is not null && await users.VerifyAsync(user, password) && !user.Disabled;
        if (!valid)
        {
            lockout.RecordFailure(userName);
            return Failed(form);
        }
        lockout.Reset(userName);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Sessions.PrincipalFor(user!));
        return form ? Results.Redirect("/") : Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return http.Request.HasFormContentType ? Results.Redirect("/login") : Results.NoContent();
    }

    /// <summary>The two fields from a form or a JSON body; anything unreadable gives nulls (a plain failure, never a 500).</summary>
    private static async Task<(string? UserName, string? Password)> ReadCredentialsAsync(HttpRequest request, bool form)
    {
        try
        {
            if (form)
            {
                var fields = await request.ReadFormAsync();
                return (fields["userName"].FirstOrDefault(), fields["password"].FirstOrDefault());
            }
            var body = await request.ReadFromJsonAsync<LoginRequest>();
            return (body?.UserName, body?.Password);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or InvalidDataException or BadHttpRequestException or IOException)
        {
            return (null, null);
        }
    }

    private static IResult Failed(bool form) => form
        ? Results.Content(Pages.Login(failed: true), "text/html", statusCode: StatusCodes.Status401Unauthorized)
        : Results.Json(new ErrorResponse(Failure), statusCode: StatusCodes.Status401Unauthorized);
}
