using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Accounts;

/// <summary>Accounts (spec §5): the rules for names and passwords, hashing, and every change to a user. A change to the password,
/// the role or the disabled flag replaces the security stamp, which ends the user's sessions (spec §4.3).</summary>
internal sealed partial class UserStore(CromoDbContext db, IPasswordHasher<UserEntity> hasher, TimeProvider time)
{
    public const int MinPasswordLength = 12;

    private static readonly UserEntity Nobody = new() { UserName = "", NormalizedUserName = "", SecurityStamp = "" };
    private static string? _nobodyHash;

    public static string Normalize(string userName) => userName.ToUpperInvariant();

    public static string? UserNameProblem(string? userName) =>
        userName is not null && ValidUserName().IsMatch(userName) ? null : "A username has 3 to 24 letters, digits, '_' or '-'.";

    public static string? PasswordProblem(string? password) =>
        password is { Length: >= MinPasswordLength } ? null : $"A password has at least {MinPasswordLength} characters.";

    public Task<UserEntity?> FindAsync(string userName, CancellationToken cancel = default)
    {
        var normalized = Normalize(userName);
        return db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalized, cancel);
    }

    public Task<UserEntity?> FindAsync(int id, CancellationToken cancel = default) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancel);

    public Task<int> ActiveAdminCountAsync(CancellationToken cancel = default) =>
        db.Users.CountAsync(u => u.IsAdmin && !u.Disabled, cancel);

    /// <summary>A new account, or why it can't be made (bad name, short password, name taken in any letter case).</summary>
    public async Task<(UserEntity? User, string? Error)> CreateAsync(string userName, string password, bool isAdmin, CancellationToken cancel = default)
    {
        if ((UserNameProblem(userName) ?? PasswordProblem(password)) is { } problem) return (null, problem);
        if (await FindAsync(userName, cancel) is not null) return (null, "That username is taken.");
        var user = new UserEntity
        {
            UserName = userName,
            NormalizedUserName = Normalize(userName),
            IsAdmin = isAdmin,
            SecurityStamp = NewStamp(),
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancel);
        return (user, null);
    }

    /// <summary>Whether the password is the user's; a hash in an older format is upgraded on the way.</summary>
    public async Task<bool> VerifyAsync(UserEntity user, string password, CancellationToken cancel = default)
    {
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancel);
        }
        return true;
    }

    /// <summary>Spends the time of a real check when there is no account to check, so response times don't reveal which names exist.</summary>
    public void VerifyNothing(string password)
    {
        _nobodyHash ??= hasher.HashPassword(Nobody, "no account has this password");
        hasher.VerifyHashedPassword(Nobody, _nobodyHash, password);
    }

    public Task SetPasswordAsync(UserEntity user, string password, CancellationToken cancel = default)
    {
        user.PasswordHash = hasher.HashPassword(user, password);
        return ChangedAsync(user, cancel);
    }

    public Task SetAdminAsync(UserEntity user, bool isAdmin, CancellationToken cancel = default)
    {
        user.IsAdmin = isAdmin;
        return ChangedAsync(user, cancel);
    }

    public Task SetDisabledAsync(UserEntity user, bool disabled, CancellationToken cancel = default)
    {
        user.Disabled = disabled;
        return ChangedAsync(user, cancel);
    }

    private Task ChangedAsync(UserEntity user, CancellationToken cancel)
    {
        user.SecurityStamp = NewStamp();
        return db.SaveChangesAsync(cancel);
    }

    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{3,24}\z")]
    private static partial Regex ValidUserName();
}
