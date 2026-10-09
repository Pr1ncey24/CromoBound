using System.Security.Claims;
using System.Text.Json;
using CromoBound.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CromoBound.Server.Accounts;

/// <summary>Sign-in, sign-out, who am I, and the home page (spec §4.4, §5.2).</summary>
internal static class LoginEndpoints
{
    public const string Failure = "Invalid username or password.";
    public const string RateLimitPolicy = "login";

    /// <summary>A sign-in body is two short fields; anything bigger is a plain failure, read no further.</summary>
    public const int MaxLoginBody = 4096;

    public static void MapAccounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", () => Results.Content(Pages.Login(failed: false), "text/html")).AllowAnonymous();
        app.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxLoginBody));
        app.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization(Policies.Seat);
        app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(user.Identity?.Name ?? "", user.IsInRole(Roles.Steward)))
            .RequireAuthorization(Policies.Seat);
        app.MapGet("/", (ClaimsPrincipal user) => Results.Content(Pages.Home(user.Identity?.Name ?? ""), "text/html"))
            .RequireAuthorization(Policies.Seat);
    }

    /// <summary>A form post (redirects home) or JSON (204). Every failure is the same answer, a locked name included; the time of a
    /// password check is spent even when the account doesn't exist or the name is locked. The attempt is reserved with the lockout
    /// before the check and always ends there: as a success only when the password was right, otherwise (an error included) as
    /// a failure.</summary>
    private static async Task<IResult> LoginAsync(HttpContext http, UserStore users, LoginLockout lockout)
    {
        var form = http.Request.HasFormContentType;
        var (userName, password) = await ReadCredentialsAsync(http.Request, form);
        if (userName is null || password is null) return Failed(form);
        if (UserStore.UserNameProblem(userName) is not null || !lockout.TryBegin(userName))
        {
            users.VerifyNothing(password);
            return Failed(form);
        }
        var valid = false;
        try
        {
            var user = await users.FindAsync(userName);
            if (user is null) users.VerifyNothing(password);
            valid = user is not null && await users.VerifyAsync(user, password) && !user.Disabled;
            if (!valid) return Failed(form);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Sessions.PrincipalFor(user!));
            return form ? Results.Redirect("/") : Results.NoContent();
        }
        finally
        {
            if (valid) lockout.Reset(userName);
            else lockout.RecordFailure(userName);
        }
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return http.Request.HasFormContentType ? Results.Redirect("/login") : Results.NoContent();
    }

    /// <summary>The two fields from a form or a JSON body of at most <see cref="MaxLoginBody"/> bytes; anything bigger or unreadable
    /// gives nulls (a plain failure, never a 500).</summary>
    private static async Task<(string? UserName, string? Password)> ReadCredentialsAsync(HttpRequest request, bool form)
    {
        try
        {
            // At most MaxLoginBody bytes are read, whatever the server or the Content-Length says; a bigger body is a failure.
            if (request.ContentLength > MaxLoginBody) return (null, null);
            var buffer = new byte[MaxLoginBody + 1];
            var length = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
            if (length > MaxLoginBody) return (null, null);
            request.Body = new MemoryStream(buffer, 0, length, writable: false);
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
