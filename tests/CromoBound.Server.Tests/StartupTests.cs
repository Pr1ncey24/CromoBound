using CromoBound.Server.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class StartupTests
{
    [Fact]
    public async Task The_first_admin_is_created_from_the_settings_with_a_hashed_password()
    {
        using var factory = new ServerFactory();
        using var scope = factory.Services.CreateScope();

        var admin = Assert.Single(await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.ToListAsync());

        Assert.Equal(ServerFactory.AdminName, admin.UserName);
        Assert.True(admin.IsAdmin);
        Assert.False(admin.Disabled);
        Assert.DoesNotContain(ServerFactory.AdminPassword, admin.PasswordHash);
        Assert.NotEmpty(admin.SecurityStamp);
    }

    [Fact]
    public void Without_users_or_first_admin_settings_the_server_refuses_to_start()
    {
        using var factory = new ServerFactory(new() { [DatabaseSetup.AdminUserKey] = "", [DatabaseSetup.AdminPasswordKey] = "" });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains(DatabaseSetup.AdminUserKey, error.ToString());
    }

    [Fact]
    public void A_first_admin_with_a_weak_password_stops_the_server()
    {
        using var factory = new ServerFactory(new() { [DatabaseSetup.AdminPasswordKey] = "short" });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("at least 12 characters", error.ToString());
    }

    [Theory]
    [InlineData("LoginRequestsPerMinute")]
    [InlineData("LockoutFailures")]
    [InlineData("LockoutMinutes")]
    [InlineData("CookieHours")]
    public void A_setting_below_one_stops_the_server_and_is_named(string setting)
    {
        using var factory = new ServerFactory(new() { [$"CromoBound:{setting}"] = "0" });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains($"CromoBound:{setting}", error.ToString());
    }

    [Fact]
    public async Task A_database_with_users_keeps_them_and_ignores_the_first_admin_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cromobound-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var first = new ServerFactory(databasePath: path)) _ = first.Services;
            using var second = new ServerFactory(new() { [DatabaseSetup.AdminUserKey] = "someone-else" }, databasePath: path);
            using var scope = second.Services.CreateScope();

            var admin = Assert.Single(await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.ToListAsync());

            Assert.Equal(ServerFactory.AdminName, admin.UserName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
