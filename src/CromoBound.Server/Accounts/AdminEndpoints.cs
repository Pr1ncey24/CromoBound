using System.Security.Claims;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Accounts;

/// <summary>User management for admins (spec §5.2). Every route needs the admin policy; a player gets a bare 403. Bodies are JSON
/// only. An admin can't demote or disable themselves, and the last active admin can't be removed.</summary>
internal static class AdminEndpoints
{
    public const string NotYourself = "You can't do that to your own account.";
    public const string LastAdmin = "The last admin can't be removed.";

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/admin/users").RequireAuthorization(Policies.Steward);
        users.MapGet("", ListAsync);
        users.MapPost("", CreateAsync);
        users.MapPut("/{id:int}/password", SetPasswordAsync);
        users.MapPut("/{id:int}/role", SetRoleAsync);
        users.MapPut("/{id:int}/disabled", SetDisabledAsync);
    }

    private static async Task<IResult> ListAsync(CromoDbContext db) =>
        Results.Ok(await db.Users.OrderBy(u => u.Id).Select(u => new UserSummary(u.Id, u.UserName, u.IsAdmin, u.Disabled)).ToListAsync());

    private static async Task<IResult> CreateAsync(CreateUserRequest request, UserStore users)
    {
        var (user, error) = await users.CreateAsync(request.UserName ?? "", request.Password ?? "", request.IsAdmin);
        return user is null ? Results.BadRequest(new ErrorResponse(error!)) : Results.Created($"/api/admin/users/{user.Id}", Summary(user));
    }

    /// <summary>Ends the user's sessions; an admin changing their own password is signed in again with the new stamp.</summary>
    private static async Task<IResult> SetPasswordAsync(int id, PasswordRequest request, UserStore users, HttpContext http)
    {
        if (await users.FindAsync(id) is not { } user) return Results.NotFound();
        if (UserStore.PasswordProblem(request.Password) is { } problem) return Results.BadRequest(new ErrorResponse(problem));
        await users.SetPasswordAsync(user, request.Password!);
        if (Sessions.UserId(http.User) == user.Id)
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Sessions.PrincipalFor(user));
        return Results.NoContent();
    }

    private static async Task<IResult> SetRoleAsync(int id, RoleRequest request, UserStore users, ClaimsPrincipal me)
    {
        if (await users.FindAsync(id) is not { } user) return Results.NotFound();
        if (!request.IsAdmin && await RefusalAsync(user, me, users) is { } problem) return Results.BadRequest(new ErrorResponse(problem));
        await users.SetAdminAsync(user, request.IsAdmin);
        return Results.NoContent();
    }

    private static async Task<IResult> SetDisabledAsync(int id, DisabledRequest request, UserStore users, ClaimsPrincipal me)
    {
        if (await users.FindAsync(id) is not { } user) return Results.NotFound();
        if (request.Disabled && await RefusalAsync(user, me, users) is { } problem) return Results.BadRequest(new ErrorResponse(problem));
        await users.SetDisabledAsync(user, request.Disabled);
        return Results.NoContent();
    }

    /// <summary>Why a demotion or a disable can't be made: never to your own account, and never to the last active admin.</summary>
    private static async Task<string?> RefusalAsync(UserEntity user, ClaimsPrincipal me, UserStore users)
    {
        if (Sessions.UserId(me) == user.Id) return NotYourself;
        if (user is { IsAdmin: true, Disabled: false } && await users.ActiveAdminCountAsync() <= 1) return LastAdmin;
        return null;
    }

    private static UserSummary Summary(UserEntity user) => new(user.Id, user.UserName, user.IsAdmin, user.Disabled);
}
