using CromoBound.Server.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>Runs before the server accepts requests: brings the database up to the latest migration and, while there are no users, creates the first
/// admin from <see cref="AdminUserKey"/> and <see cref="AdminPasswordKey"/> (spec §5.3). Without users and without those
/// settings the server refuses to start.</summary>
internal sealed class DatabaseSetup(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DatabaseSetup> log) : IHostedService
{
    public const string AdminUserKey = "CROMOBOUND_ADMIN_USER";
    public const string AdminPasswordKey = "CROMOBOUND_ADMIN_PASSWORD";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CromoDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken)) return;

        var userName = configuration[AdminUserKey];
        var password = configuration[AdminPasswordKey];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException($"There are no users yet: set {AdminUserKey} and {AdminPasswordKey} to create the first admin.");
        var (user, error) = await scope.ServiceProvider.GetRequiredService<UserStore>().CreateAsync(userName, password, isAdmin: true, cancellationToken);
        if (user is null) throw new InvalidOperationException($"The first admin can't be created: {error}");
        log.LogInformation("Created the first admin account {UserName}.", user.UserName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
