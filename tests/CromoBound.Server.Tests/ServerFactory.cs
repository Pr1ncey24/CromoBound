using System.Net;
using System.Net.Http.Json;
using CromoBound.Data;
using CromoBound.Engine.Tests;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The whole server in memory, in the Production environment, over its own temporary SQLite file, with a first admin
/// and the engine tests' small card pool. Clients use https, so the Secure session cookie is sent back.</summary>
internal sealed class ServerFactory : WebApplicationFactory<Program>
{
    public const string AdminName = "admin";
    public const string AdminPassword = "admin-password-1";
    public const string PlayerPassword = "player-password-1";

    /// <summary>The card pool test servers use unless they load the real card data.</summary>
    public static CardDatabase TestCards { get; } = EngineTestDb.Create();

    private readonly Dictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _services;
    private readonly bool _ownsDatabase;
    private readonly bool _loadCards;

    /// <summary><paramref name="settings"/> override the defaults; <paramref name="services"/> replaces services (e.g. the clock);
    /// a given <paramref name="databasePath"/> is kept after the factory is disposed; <paramref name="loadCards"/> loads the card
    /// data from <c>CromoBound:DataFolder</c> as the real server does, instead of using <see cref="TestCards"/>.</summary>
    public ServerFactory(Dictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null, string? databasePath = null,
        bool loadCards = false)
    {
        _ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? NewDatabasePath();
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
        _loadCards = loadCards;
    }

    public string DatabasePath { get; }

    /// <summary>A path for a new temporary database file.</summary>
    public static string NewDatabasePath() => Path.Combine(Path.GetTempPath(), $"cromobound-test-{Guid.NewGuid():N}.db");

    /// <summary>Closes the pooled connections of this database only (other tests' servers run in parallel) and deletes it.</summary>
    public static void DeleteDatabase(string path)
    {
        using (var connection = new SqliteConnection($"Data Source={path}")) SqliteConnection.ClearPool(connection);
        File.Delete(path);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureTestServices(services =>
        {
            if (!_loadCards) services.AddSingleton(TestCards);
            _services?.Invoke(services);
        });
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

    /// <summary>Signs in and returns the session cookie as a request header value ("name=value"), for a hub connection.</summary>
    public async Task<string> SessionCookieAsync(string userName, string password)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false,
        });
        var response = await client.PostAsJsonAsync("/login", new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-session=", StringComparison.Ordinal)).Split(';')[0];
    }

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

    /// <summary>Runs code against the database in its own scope, e.g. to read or edit a match row.</summary>
    public async Task WithDbAsync(Func<CromoDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<CromoDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase) DeleteDatabase(DatabasePath);
    }
}
