using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The whole server in memory over its own temporary SQLite file, with a first admin. Clients use https, so the Secure
/// session cookie is sent back.</summary>
internal sealed class ServerFactory : WebApplicationFactory<Program>
{
    public const string AdminName = "admin";
    public const string AdminPassword = "admin-password-1";
    public const string PlayerPassword = "player-password-1";

    private readonly Dictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _services;
    private readonly bool _ownsDatabase;

    /// <summary><paramref name="settings"/> override the defaults; <paramref name="services"/> replaces services (e.g. the clock);
    /// a given <paramref name="databasePath"/> is kept after the factory is disposed.</summary>
    public ServerFactory(Dictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null, string? databasePath = null)
    {
        _ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"cromobound-test-{Guid.NewGuid():N}.db");
        _settings = new()
        {
            ["CromoBound:DatabasePath"] = DatabasePath,
            ["CromoBound:LoginRequestsPerMinute"] = "100",
            [DatabaseSetup.AdminUserKey] = AdminName,
            [DatabaseSetup.AdminPasswordKey] = AdminPassword,
        };
        if (settings is not null)
            foreach (var (key, value) in settings) _settings[key] = value;
        _services = services;
    }

    public string DatabasePath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));
        if (_services is not null) builder.ConfigureTestServices(_services);
    }

    /// <summary>A client with its own cookies that doesn't follow redirects.</summary>
    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public async Task<HttpClient> SignInAsync(string userName, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/login", new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    public Task<HttpClient> SignInAdminAsync() => SignInAsync(AdminName, AdminPassword);

    /// <summary>Adds a user with <see cref="PlayerPassword"/> straight through the store.</summary>
    public async Task<UserEntity> AddUserAsync(string userName, bool isAdmin = false)
    {
        using var scope = Services.CreateScope();
        var (user, error) = await scope.ServiceProvider.GetRequiredService<UserStore>().CreateAsync(userName, PlayerPassword, isAdmin);
        Assert.True(error is null, error);
        return user!;
    }

    /// <summary>Runs code against the store in its own scope, e.g. to disable a user.</summary>
    public async Task WithStoreAsync(Func<UserStore, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<UserStore>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !_ownsDatabase) return;
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }
}
