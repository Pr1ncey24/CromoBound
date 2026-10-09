# Server Plan G: Foundation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. First of two plans for Phase 3 part 1 (G: foundation; H: matches).

**Goal:** A server where the only public thing is a login page. An admin-created account signs in with a protected cookie; every other route needs one of two roles; logins are rate-limited and guessed usernames lock out without revealing anything; admins manage users over HTTP.

**Architecture:**
- **`CromoBound.Server`:** an ASP.NET Core app over SQLite through EF Core. Its parts:
  - a `UserStore` for accounts;
  - cookie authentication, which checks each user's security stamp on every request;
  - two authorization policies and a fallback policy that denies by default;
  - a fixed-window rate limiter on `/login` plus an in-memory per-username lockout;
  - minimal-API endpoints.
- **First start:** a hosted service creates the database and the first admin from environment variables.
- **`CromoBound.Server.Tests`:** runs the whole server in memory with `WebApplicationFactory` and real HTTP clients.

**Tech Stack:**
- .NET 10 (ASP.NET Core 10.0.12).
- Microsoft.EntityFrameworkCore.Sqlite 10.0.12.
- Microsoft.Extensions.Identity.Core 10.0.12, for its password hasher only.
- Tests: xUnit 2.9.3 and Microsoft.AspNetCore.Mvc.Testing 10.0.12.

**Spec:** `docs/server.md` (sections 1-5, 7, 9, 10 row G).

## Global Constraints

- **Language and placement:** `net10.0` with nullable enabled (from `Directory.Build.props`). Server types are `internal` unless something outside must see them (`Program`, and the request/response records that cross HTTP).
- **Default deny (spec §4.1):**
  - Every endpoint needs a signed-in user with the player role, except `GET /login` and `POST /login`.
  - Signed out: browser page requests are redirected to `/login`; everything else gets a bare 401.
  - Signed in without the role: a bare 403.
- **Role identifiers (spec §4.2):** specific constants in `Accounts/Roles.cs`. No response body, header, log line or error message ever contains a role identifier or role name.
- **Login failures (spec §4.4):** every failure returns the same answer, "Invalid username or password." Responses never reveal whether a username exists or is locked.
- **Logging:** never log a password or a password hash.
- **Privacy:**
  - The login page says nothing about what the site is: no "CromoBound", no "Riftbound".
  - Every response carries `X-Robots-Tag: noindex, nofollow`.
  - The session cookie has a generic name.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments or strings.
  - LF line endings; UTF-8 without BOM.
- **dotnet:** run it with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash. The default dotnet on PATH is SDK 9.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **`EnsureCreated` instead of migrations for now.** There is no deployed database before Plan H. Plan H switches to EF Core migrations, with an initial migration holding both tables, before the first deployment.
2. **The session check reads the database on every request, without a cache.** Spec §4.3 allowed a brief cache. One indexed lookup per request is cheap for a few users, and it removes any window in which a changed account keeps working.
3. **The login and home pages are served from code (`Accounts/Pages.cs`), not from `wwwroot`.** That way any static files Phase 4 adds stay behind the fallback policy.
4. **The redirect to the login page carries no return URL.** It always lands on `/` after sign-in.
5. **Loading card data moves to Plan H**, which is where matches need it.
6. **No test covers forwarded headers or the last-admin rule.**
   - The test host has no remote address, so forwarded headers can't be exercised.
   - The last-admin rule can't be reached: the only way to remove the last admin is to act on your own account, which the self rule already refuses. The rule stays in code as a backstop.
7. **The test factory sets the login limit to 100 requests a minute**, so tests that sign in many times aren't throttled. The rate-limit test sets its own low limit.

## Review Focus

1. **Usernames that differ only in case** ("Admin", "admin") are one account. Signing in works in any case, and a second account can't be created with another case. Pinned in Task 1 (`Names_are_unique_whatever_their_case`) and Task 2 (`Signing_in_ignores_the_case_of_the_username`).
2. **A session after its account changes.** Once a password changes, a role changes or the account is disabled, the old cookie stops working on the very next request. Pinned in Task 2 (`A_changed_account_ends_its_sessions_at_once`) and Task 4 (`A_new_password_works_and_ends_the_users_sessions`, `Promoting_demoting_and_disabling_end_the_users_sessions`).
3. **A malformed sign-in** (bad JSON, the wrong content type, wrong field types) is a plain failure, never a 500. Pinned in Task 2 (`A_malformed_sign_in_is_a_plain_failure`).
4. **A locked username looks exactly like a wrong password**, including in letter case, and the lock ends on time. Pinned in Task 3 (`Five_failures_lock_a_username_without_saying_so`, `The_lockout_ends_after_its_time`).
5. **An admin locking themselves out.** Demoting or disabling yourself is refused; changing your own password keeps you signed in. Pinned in Task 4 (`An_admin_cant_demote_or_disable_themselves_and_stays_signed_in_after_changing_their_own_password`).

---

## File Structure

```
src/CromoBound.Server/
  CromoBound.Server.csproj        web project; EF Core SQLite, Identity.Core
  Program.cs                      composition root
  ServerOptions.cs                settings bound from the "CromoBound" section
  ServerErrors.cs                 the generic 500 writer
  Privacy.cs                      noindex header
  Proxies.cs                      forwarded headers from known proxies
  appsettings.json
  Storage/UserEntity.cs           the user row
  Storage/CromoDbContext.cs       EF Core context
  Storage/StorageSetup.cs         service registration
  Storage/DatabaseSetup.cs        creates the database and the first admin at startup
  Accounts/UserStore.cs           name and password rules, hashing, user changes
  Accounts/Roles.cs               role identifiers and policy names
  Accounts/Contracts.cs           login and me records
  Accounts/AdminContracts.cs      admin records
  Accounts/Sessions.cs            principal, stamp validation, challenge/forbid responses
  Accounts/AccountsSetup.cs       cookie auth, policies, data protection, rate limiter, lockout
  Accounts/Pages.cs               login and home HTML
  Accounts/LoginEndpoints.cs      /login, /logout, /api/me, /
  Accounts/LoginLockout.cs        per-username lockout
  Accounts/AdminEndpoints.cs      /api/admin/users
tests/CromoBound.Server.Tests/
  CromoBound.Server.Tests.csproj
  ServerFactory.cs, ManualTime.cs
  StartupTests.cs, UserStoreTests.cs, SignInTests.cs, DefaultDenyTests.cs, ErrorTests.cs,
  LoginProtectionTests.cs, AdminTests.cs
CromoBound.slnx, .gitignore      (modify)
```

---

### Task 1: The server project, the database and the first admin

**Files:**
- Create: `src/CromoBound.Server/CromoBound.Server.csproj`, `Program.cs`, `ServerOptions.cs`, `ServerErrors.cs`, `appsettings.json`
- Create: `src/CromoBound.Server/Storage/UserEntity.cs`, `CromoDbContext.cs`, `StorageSetup.cs`, `DatabaseSetup.cs`
- Create: `src/CromoBound.Server/Accounts/UserStore.cs`, `src/CromoBound.Server/Accounts/Contracts.cs` (only `LoginRequest`; Task 2 completes it)
- Create: `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, `ServerFactory.cs`, `StartupTests.cs`, `UserStoreTests.cs`
- Modify: `CromoBound.slnx`, `.gitignore`

**Interfaces:**
- Produces:
  - `ServerOptions` (`DatabasePath`, `DataFolder`, `KeysFolder`, `LoginRequestsPerMinute`, `LockoutFailures`, `LockoutMinutes`, `CookieHours`, `KnownProxies`; section `"CromoBound"`).
  - `UserEntity`, `CromoDbContext.Users`.
  - `UserStore`, with these members:
    - `Normalize`, `UserNameProblem`, `PasswordProblem`, `MinPasswordLength`;
    - `FindAsync(string)`, `FindAsync(int)`, `ActiveAdminCountAsync()`;
    - `CreateAsync(userName, password, isAdmin) : (UserEntity?, string?)`;
    - `VerifyAsync`, `VerifyNothing`;
    - `SetPasswordAsync`, `SetAdminAsync`, `SetDisabledAsync`.
  - `DatabaseSetup.AdminUserKey` = `"CROMOBOUND_ADMIN_USER"` and `DatabaseSetup.AdminPasswordKey` = `"CROMOBOUND_ADMIN_PASSWORD"`.
  - `StorageSetup.AddCromoBoundStorage(IServiceCollection, IConfiguration)`.
  - `ServerErrors.WriteGenericAsync`.
  - In the tests:
    - `ServerFactory(settings?, services?, databasePath?)`, with `NewClient()`, `SignInAsync(userName, password)`, `SignInAdminAsync()`, `AddUserAsync(userName, isAdmin)` and `WithStoreAsync(action)`;
    - the constants `AdminName`, `AdminPassword` and `PlayerPassword`.

- [ ] **Step 1: Create the projects**

`src/CromoBound.Server/CromoBound.Server.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\CromoBound.Engine\CromoBound.Engine.csproj" />
    <ProjectReference Include="..\CromoBound.Data\CromoBound.Data.csproj" />
    <ProjectReference Include="..\CromoBound.Models\CromoBound.Models.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
    <PackageReference Include="Microsoft.Extensions.Identity.Core" Version="10.0.12" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="CromoBound.Server.Tests" />
  </ItemGroup>

</Project>
```

`tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CromoBound.Server\CromoBound.Server.csproj" />
  </ItemGroup>

</Project>
```

In `CromoBound.slnx`, add `<Project Path="src/CromoBound.Server/CromoBound.Server.csproj" />` to the `/src/` folder (after `CromoBound.Models`) and `<Project Path="tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj" />` to the `/tests/` folder (after `CromoBound.Models.Tests`).

In `.gitignore`, add at the end:

```
# Local server databases
*.db
*.db-shm
*.db-wal
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Server.Tests/ServerFactory.cs`:

```csharp
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
```

This file references `LoginRequest`, which Task 2 creates. Until then, create the record in `src/CromoBound.Server/Accounts/Contracts.cs` already (Task 2 adds the rest of that file):

```csharp
namespace CromoBound.Server.Accounts;

/// <summary>A sign-in by JSON; a form post uses the same two field names.</summary>
public sealed record LoginRequest(string? UserName, string? Password);
```

Create `tests/CromoBound.Server.Tests/StartupTests.cs`:

```csharp
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
```

Create `tests/CromoBound.Server.Tests/UserStoreTests.cs`:

```csharp
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class UserStoreTests
{
    [Theory]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("Player_One-2", true)]
    [InlineData("has space", false)]
    [InlineData("way-too-long-for-a-username", false)]
    [InlineData("caf\u00e9", false)]
    public void Usernames_have_3_to_24_letters_digits_underscores_or_dashes(string userName, bool valid) =>
        Assert.Equal(valid, UserStore.UserNameProblem(userName) is null);

    [Fact]
    public void Passwords_need_at_least_12_characters()
    {
        Assert.NotNull(UserStore.PasswordProblem("eleven-char"));
        Assert.NotNull(UserStore.PasswordProblem(null));
        Assert.Null(UserStore.PasswordProblem("twelve-chars"));
    }

    [Fact]
    public async Task Names_are_unique_whatever_their_case()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("Player1");

        await factory.WithStoreAsync(async store =>
        {
            var (user, error) = await store.CreateAsync("PLAYER1", ServerFactory.PlayerPassword, isAdmin: false);

            Assert.Null(user);
            Assert.Equal("That username is taken.", error);
            Assert.Equal("Player1", (await store.FindAsync("player1"))?.UserName);
        });
    }

    [Fact]
    public async Task A_password_change_replaces_the_hash_and_the_security_stamp()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");

        await factory.WithStoreAsync(async store =>
        {
            var user = (await store.FindAsync("player1"))!;
            var (hash, stamp) = (user.PasswordHash, user.SecurityStamp);

            await store.SetPasswordAsync(user, "a-brand-new-password");

            Assert.NotEqual(hash, user.PasswordHash);
            Assert.NotEqual(stamp, user.SecurityStamp);
            Assert.True(await store.VerifyAsync(user, "a-brand-new-password"));
            Assert.False(await store.VerifyAsync(user, ServerFactory.PlayerPassword));
        });
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/CromoBound.Server.Tests`
Expected: FAIL (build errors: `Program`, `DatabaseSetup`, `CromoDbContext`, `UserStore` don't exist).

- [ ] **Step 4: Write the implementation**

`src/CromoBound.Server/ServerOptions.cs`:

```csharp
namespace CromoBound.Server;

/// <summary>Server settings from the "CromoBound" configuration section (spec §7). Environment variables override them
/// (e.g. <c>CromoBound__DatabasePath</c>).</summary>
internal sealed class ServerOptions
{
    public const string Section = "CromoBound";

    public string DatabasePath { get; set; } = "cromobound.db";
    public string DataFolder { get; set; } = "data";

    /// <summary>Where the keys that encrypt the session cookie are kept; unset means the framework's default folder.</summary>
    public string? KeysFolder { get; set; }

    public int LoginRequestsPerMinute { get; set; } = 10;
    public int LockoutFailures { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int CookieHours { get; set; } = 12;

    /// <summary>Addresses of the reverse proxies whose forwarded headers are trusted.</summary>
    public List<string> KnownProxies { get; set; } = [];
}
```

`src/CromoBound.Server/ServerErrors.cs`:

```csharp
namespace CromoBound.Server;

/// <summary>Unexpected errors (spec §4.5): a generic 500 with no details; the exception itself goes to the log.</summary>
internal static class ServerErrors
{
    public const string Generic = "Something went wrong.";

    public static Task WriteGenericAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(Generic);
    }
}
```

`src/CromoBound.Server/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "CromoBound": {
    "DatabasePath": "cromobound.db",
    "DataFolder": "data",
    "LoginRequestsPerMinute": 10,
    "LockoutFailures": 5,
    "LockoutMinutes": 15,
    "CookieHours": 12,
    "KnownProxies": []
  }
}
```

`src/CromoBound.Server/Storage/UserEntity.cs`:

```csharp
namespace CromoBound.Server.Storage;

/// <summary>An account (spec §5.1). <see cref="SecurityStamp"/> changes whenever the password, the role or the disabled flag
/// changes; sessions carrying an older stamp end.</summary>
internal sealed class UserEntity
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public required string NormalizedUserName { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool Disabled { get; set; }
    public required string SecurityStamp { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`src/CromoBound.Server/Storage/CromoDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>The server's SQLite database.</summary>
internal sealed class CromoDbContext(DbContextOptions<CromoDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<UserEntity>();
        user.ToTable("Users");
        user.Property(u => u.UserName).HasMaxLength(24);
        user.Property(u => u.NormalizedUserName).HasMaxLength(24);
        user.HasIndex(u => u.NormalizedUserName).IsUnique();
    }
}
```

`src/CromoBound.Server/Storage/StorageSetup.cs`:

```csharp
using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Storage;

internal static class StorageSetup
{
    /// <summary>Settings, the database, the clock, password hashing, the user store, and the startup setup.</summary>
    public static IServiceCollection AddCromoBoundStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.Section));
        services.AddDbContext<CromoDbContext>((provider, db) =>
            db.UseSqlite($"Data Source={provider.GetRequiredService<IOptions<ServerOptions>>().Value.DatabasePath}"));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
        services.AddScoped<UserStore>();
        services.AddHostedService<DatabaseSetup>();
        return services;
    }
}
```

`src/CromoBound.Server/Storage/DatabaseSetup.cs`:

```csharp
using CromoBound.Server.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>Runs before the server accepts requests: creates the database if needed and, while there are no users, the first
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
        await db.Database.EnsureCreatedAsync(cancellationToken);
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
```

`src/CromoBound.Server/Accounts/UserStore.cs`:

```csharp
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

    [GeneratedRegex("^[A-Za-z0-9_-]{3,24}$")]
    private static partial Regex ValidUserName();
}
```

`src/CromoBound.Server/Program.cs`:

```csharp
using CromoBound.Server;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCromoBoundStorage(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including the engine and model suites).

- [ ] **Step 6: Commit**

```bash
git add CromoBound.slnx .gitignore src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): add the server project, database and first admin"
```

---

### Task 2: Cookie sign-in and default deny

**Files:**
- Create: `src/CromoBound.Server/Accounts/Roles.cs`, `Sessions.cs`, `AccountsSetup.cs`, `Pages.cs`, `LoginEndpoints.cs`
- Modify: `src/CromoBound.Server/Accounts/Contracts.cs`
- Create: `src/CromoBound.Server/Privacy.cs`
- Modify: `src/CromoBound.Server/Program.cs`
- Test: `tests/CromoBound.Server.Tests/SignInTests.cs`, `DefaultDenyTests.cs`, `ErrorTests.cs`

**Interfaces:**
- Consumes: Task 1's `UserStore`, `UserEntity`, `ServerOptions`, `ServerFactory`.
- Produces:
  - `Roles.Seat` and `Roles.Steward` (claim values), and `Policies.Seat` and `Policies.Steward` (policy names).
  - `Sessions.PrincipalFor(UserEntity)`, `Sessions.UserId(ClaimsPrincipal)` and `Sessions.ValidateAsync`.
  - `AccountsSetup.AddCromoBoundAccounts(IServiceCollection, IConfiguration)`.
  - `LoginEndpoints.MapAccounts(IEndpointRouteBuilder)` and `LoginEndpoints.Failure`.
  - The records `ErrorResponse(string Error)` and `MeResponse(string UserName, bool CanManageUsers)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Server.Tests/SignInTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class SignInTests
{
    private static HttpRequestMessage Page(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("text/html");
        return request;
    }

    private static FormUrlEncodedContent Form(string userName, string password) =>
        new(new Dictionary<string, string> { ["userName"] = userName, ["password"] = password });

    [Fact]
    public async Task Signed_out_pages_go_to_the_login_page_and_everything_else_gets_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        foreach (var path in new[] { "/", "/no-such-page" })
        {
            var response = await client.SendAsync(Page(path));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login", response.Headers.Location?.OriginalString);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Page("/api/me"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/logout", null)).StatusCode);
    }

    [Fact]
    public async Task The_login_page_is_public_and_doesnt_say_what_the_site_is()
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().SendAsync(Page("/login"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<form method=\"post\" action=\"/login\">", html);
        Assert.DoesNotContain("cromobound", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riftbound", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("noindex, nofollow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }

    [Fact]
    public async Task Me_says_who_you_are_and_whether_you_manage_users()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");

        var admin = await (await factory.SignInAdminAsync()).GetFromJsonAsync<MeResponse>("/api/me");
        var player = await (await factory.SignInAsync("player1", ServerFactory.PlayerPassword)).GetFromJsonAsync<MeResponse>("/api/me");

        Assert.Equal(new MeResponse(ServerFactory.AdminName, true), admin);
        Assert.Equal(new MeResponse("player1", false), player);
    }

    [Fact]
    public async Task Signing_in_ignores_the_case_of_the_username()
    {
        using var factory = new ServerFactory();

        var client = await factory.SignInAsync(ServerFactory.AdminName.ToUpperInvariant(), ServerFactory.AdminPassword);

        Assert.Equal(ServerFactory.AdminName, (await client.GetFromJsonAsync<MeResponse>("/api/me"))!.UserName);
    }

    [Fact]
    public async Task A_form_sign_in_goes_home_and_a_failed_one_shows_the_generic_message()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        var signedIn = await client.PostAsync("/login", Form(ServerFactory.AdminName, ServerFactory.AdminPassword));
        var home = await client.SendAsync(Page("/"));
        var failed = await factory.NewClient().PostAsync("/login", Form(ServerFactory.AdminName, "wrong-password-1"));

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains($"Signed in as {ServerFactory.AdminName}", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.Contains(LoginEndpoints.Failure, await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_failed_sign_in_gives_the_same_answer_whatever_the_reason()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("gone");
        await factory.WithStoreAsync(async store => await store.SetDisabledAsync((await store.FindAsync("gone"))!, true));
        var answers = new List<(HttpStatusCode Status, string Body)>();

        foreach (var (userName, password) in new[]
        {
            (ServerFactory.AdminName, "wrong-password-1"),
            ("nobody-here", "wrong-password-1"),
            ("gone", ServerFactory.PlayerPassword),
        })
        {
            var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(userName, password));
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
        }

        Assert.All(answers, answer => Assert.Equal(answers[0], answer));
        Assert.Equal(HttpStatusCode.Unauthorized, answers[0].Status);
        Assert.Contains(LoginEndpoints.Failure, answers[0].Body);
    }

    [Theory]
    [InlineData("not json", "application/json")]
    [InlineData("""{ "userName": 5 }""", "application/json")]
    [InlineData("userName=admin", "text/plain")]
    public async Task A_malformed_sign_in_is_a_plain_failure(string body, string contentType)
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().PostAsync("/login", new StringContent(body, Encoding.UTF8, contentType));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(LoginEndpoints.Failure, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_secure_and_strict()
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(ServerFactory.AdminName, ServerFactory.AdminPassword));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.StartsWith("__Host-session=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        using var factory = new ServerFactory();
        var client = await factory.SignInAdminAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/logout", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task A_changed_account_ends_its_sessions_at_once()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        await factory.AddUserAsync("player2");
        var first = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var second = await factory.SignInAsync("player2", ServerFactory.PlayerPassword);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/me")).StatusCode);

        await factory.WithStoreAsync(async store =>
        {
            await store.SetPasswordAsync((await store.FindAsync("player1"))!, "a-brand-new-password");
            await store.SetDisabledAsync((await store.FindAsync("player2"))!, true);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/me")).StatusCode);
    }
}
```

Create `tests/CromoBound.Server.Tests/DefaultDenyTests.cs`:

```csharp
using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Tests;

public class DefaultDenyTests
{
    [Fact]
    public void Every_endpoint_but_the_login_endpoints_requires_a_role()
    {
        using var factory = new ServerFactory();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText;
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                Assert.Equal("/login", route);
                continue;
            }
            Assert.True(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0, $"{route} has no role requirement.");
        }
    }

    [Fact]
    public void Anything_without_its_own_rule_needs_the_player_role()
    {
        using var factory = new ServerFactory();

        var fallback = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        Assert.NotNull(fallback);
        var roles = Assert.Single(fallback.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(new[] { Roles.Seat }, roles.AllowedRoles);
    }
}
```

Create `tests/CromoBound.Server.Tests/ErrorTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class ErrorTests
{
    /// <summary>Hashes normally (so the first admin is created) but fails when checking a password.</summary>
    private sealed class BrokenHasher : PasswordHasher<UserEntity>
    {
        public override PasswordVerificationResult VerifyHashedPassword(UserEntity user, string hashedPassword, string providedPassword) =>
            throw new InvalidOperationException("secret detail from inside the server");
    }

    [Fact]
    public async Task An_unexpected_error_returns_a_generic_500_without_details()
    {
        using var factory = new ServerFactory(services: services => services.AddSingleton<IPasswordHasher<UserEntity>, BrokenHasher>());

        var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(ServerFactory.AdminName, ServerFactory.AdminPassword));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Something went wrong.", await response.Content.ReadAsStringAsync());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/CromoBound.Server.Tests`
Expected: FAIL (build errors: `MeResponse`, `LoginEndpoints`, `Roles` don't exist).

- [ ] **Step 3: Write the implementation**

`src/CromoBound.Server/Accounts/Roles.cs`:

```csharp
namespace CromoBound.Server.Accounts;

/// <summary>The role claim values (spec §4.2): specific identifiers, never "Administrator" or "Player", and never written to a
/// response, a log or an error. An admin holds both, so admin implies player.</summary>
internal static class Roles
{
    public const string Seat = "cb-seat-2b8a57c4e019";
    public const string Steward = "cb-steward-6d1f0e93a74c";
}

/// <summary>Authorization policy names; internal, never written to a response either.</summary>
internal static class Policies
{
    public const string Seat = "seat";
    public const string Steward = "steward";
}
```

Replace `src/CromoBound.Server/Accounts/Contracts.cs` with:

```csharp
namespace CromoBound.Server.Accounts;

/// <summary>A sign-in by JSON; a form post uses the same two field names.</summary>
public sealed record LoginRequest(string? UserName, string? Password);

public sealed record ErrorResponse(string Error);

/// <summary>Who is signed in, and whether the UI should show user management (never a role name).</summary>
public sealed record MeResponse(string UserName, bool CanManageUsers);
```

`src/CromoBound.Server/Accounts/Sessions.cs`:

```csharp
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

    /// <summary>On every request: the account must still exist, be enabled and carry the same security stamp; otherwise the session
    /// ends (a changed password, role or disabled flag takes effect at once).</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal!;
        var id = UserId(principal);
        var stamp = principal.FindFirstValue(StampClaim);
        var db = context.HttpContext.RequestServices.GetRequiredService<CromoDbContext>();
        var user = id is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        if (user is { Disabled: false } && user.SecurityStamp == stamp) return;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
```

`src/CromoBound.Server/Accounts/AccountsSetup.cs`:

```csharp
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
```

`src/CromoBound.Server/Accounts/Pages.cs`:

```csharp
using System.Text.Encodings.Web;

namespace CromoBound.Server.Accounts;

/// <summary>The two server-made pages. The login page names nothing about the site (spec §1); the home page is a placeholder
/// until the Phase 4 UI.</summary>
internal static class Pages
{
    public static string Login(bool failed) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>Sign in</title>
        <style>
        body { font-family: system-ui, sans-serif; display: grid; place-items: center; min-height: 100vh; margin: 0; }
        form { display: grid; gap: 0.75rem; width: min(20rem, 90vw); }
        input, button { font: inherit; padding: 0.5rem; }
        </style>
        </head>
        <body>
        <main>
        <h1>Sign in</h1>
        {{(failed ? $"<p role=\"alert\">{LoginEndpoints.Failure}</p>" : "")}}
        <form method="post" action="/login">
        <label>Username <input name="userName" autocomplete="username" required></label>
        <label>Password <input name="password" type="password" autocomplete="current-password" required></label>
        <button type="submit">Sign in</button>
        </form>
        </main>
        </body>
        </html>
        """;

    public static string Home(string userName) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>CromoBound</title>
        </head>
        <body>
        <main>
        <p>Signed in as {{HtmlEncoder.Default.Encode(userName)}}.</p>
        <form method="post" action="/logout"><button type="submit">Sign out</button></form>
        </main>
        </body>
        </html>
        """;
}
```

`src/CromoBound.Server/Accounts/LoginEndpoints.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CromoBound.Server.Accounts;

/// <summary>Sign-in, sign-out, who am I, and the home page (spec §4.4, §5.2).</summary>
internal static class LoginEndpoints
{
    public const string Failure = "Invalid username or password.";

    public static void MapAccounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", () => Results.Content(Pages.Login(failed: false), "text/html")).AllowAnonymous();
        app.MapPost("/login", LoginAsync).AllowAnonymous();
        app.MapPost("/logout", LogoutAsync).RequireAuthorization(Policies.Seat);
        app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(user.Identity?.Name ?? "", user.IsInRole(Roles.Steward)))
            .RequireAuthorization(Policies.Seat);
        app.MapGet("/", (ClaimsPrincipal user) => Results.Content(Pages.Home(user.Identity?.Name ?? ""), "text/html"))
            .RequireAuthorization(Policies.Seat);
    }

    /// <summary>A form post (redirects home) or JSON (204). Every failure is the same answer; the time of a password check is spent
    /// even when the account doesn't exist.</summary>
    private static async Task<IResult> LoginAsync(HttpContext http, UserStore users)
    {
        var form = http.Request.HasFormContentType;
        var (userName, password) = await ReadCredentialsAsync(http.Request, form);
        if (userName is null || password is null) return Failed(form);
        if (UserStore.UserNameProblem(userName) is not null)
        {
            users.VerifyNothing(password);
            return Failed(form);
        }
        var user = await users.FindAsync(userName);
        if (user is null) users.VerifyNothing(password);
        var valid = user is not null && await users.VerifyAsync(user, password) && !user.Disabled;
        if (!valid) return Failed(form);
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
        if (form)
        {
            var fields = await request.ReadFormAsync();
            return (fields["userName"].FirstOrDefault(), fields["password"].FirstOrDefault());
        }
        try
        {
            var body = await request.ReadFromJsonAsync<LoginRequest>();
            return (body?.UserName, body?.Password);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return (null, null);
        }
    }

    private static IResult Failed(bool form) => form
        ? Results.Content(Pages.Login(failed: true), "text/html", statusCode: StatusCodes.Status401Unauthorized)
        : Results.Json(new ErrorResponse(Failure), statusCode: StatusCodes.Status401Unauthorized);
}
```

`src/CromoBound.Server/Privacy.cs`:

```csharp
namespace CromoBound.Server;

/// <summary>Only the owner's friends should know what the site is (spec §1): every response asks search engines not to index it.</summary>
internal static class Privacy
{
    public static Task NoIndexAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return next(context);
    }
}
```

Replace `src/CromoBound.Server/Program.cs` with:

```csharp
using CromoBound.Server;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.AddCromoBoundStorage(builder.Configuration);
builder.Services.AddCromoBoundAccounts(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
app.Use(Privacy.NoIndexAsync);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccounts();
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): add cookie sign-in and default deny"
```

---

### Task 3: Rate-limited logins and username lockout

**Files:**
- Create: `src/CromoBound.Server/Accounts/LoginLockout.cs`, `src/CromoBound.Server/Proxies.cs`
- Modify: `src/CromoBound.Server/Accounts/AccountsSetup.cs`, `src/CromoBound.Server/Accounts/LoginEndpoints.cs`, `src/CromoBound.Server/Program.cs`
- Test: `tests/CromoBound.Server.Tests/ManualTime.cs`, `LoginProtectionTests.cs`

**Interfaces:**
- Consumes: Task 1's `ServerOptions` (`LoginRequestsPerMinute`, `LockoutFailures`, `LockoutMinutes`, `KnownProxies`) and `UserStore.Normalize`; Task 2's `LoginEndpoints` and `AccountsSetup`.
- Produces:
  - `LoginLockout` with `IsLocked(userName)`, `RecordFailure(userName)` and `Reset(userName)`.
  - `LoginEndpoints.RateLimitPolicy` = `"login"`.
  - `Proxies.AddCromoBoundProxies(IServiceCollection)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Server.Tests/ManualTime.cs`:

```csharp
namespace CromoBound.Server.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}
```

Create `tests/CromoBound.Server.Tests/LoginProtectionTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class LoginProtectionTests
{
    private const string Wrong = "wrong-password-1";

    private static Task<HttpResponseMessage> TryAsync(ServerFactory factory, string userName, string password) =>
        factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(userName, password));

    private static async Task<(HttpStatusCode Status, string Body)> AnswerAsync(Task<HttpResponseMessage> request)
    {
        var response = await request;
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Too_many_login_requests_from_one_address_get_a_bare_429()
    {
        using var factory = new ServerFactory(new() { ["CromoBound:LoginRequestsPerMinute"] = "3" });
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await TryAsync(factory, "nobody-here", Wrong)).StatusCode);

        var limited = await TryAsync(factory, ServerFactory.AdminName, ServerFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Empty(await limited.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Five_failures_lock_a_username_without_saying_so()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        var wrong = await AnswerAsync(TryAsync(factory, "player1", Wrong));
        for (var i = 0; i < 4; i++) await TryAsync(factory, "player1", Wrong);

        var locked = await AnswerAsync(TryAsync(factory, "player1", ServerFactory.PlayerPassword));
        var lockedOtherCase = await AnswerAsync(TryAsync(factory, "PLAYER1", ServerFactory.PlayerPassword));

        Assert.Equal(wrong, locked);
        Assert.Equal(wrong, lockedOtherCase);
        Assert.Equal(HttpStatusCode.NoContent, (await TryAsync(factory, ServerFactory.AdminName, ServerFactory.AdminPassword)).StatusCode);
    }

    [Fact]
    public async Task Unknown_names_lock_the_same_way()
    {
        using var factory = new ServerFactory();
        for (var i = 0; i < 5; i++) await TryAsync(factory, "player1", Wrong);
        await factory.AddUserAsync("player1");

        Assert.Equal(HttpStatusCode.Unauthorized, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);
    }

    [Fact]
    public async Task The_lockout_ends_after_its_time()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        using var factory = new ServerFactory(services: services => services.AddSingleton<TimeProvider>(time));
        await factory.AddUserAsync("player1");
        for (var i = 0; i < 5; i++) await TryAsync(factory, "player1", Wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);

        time.Now += TimeSpan.FromMinutes(15);

        Assert.Equal(HttpStatusCode.NoContent, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);
    }

    [Fact]
    public async Task A_successful_sign_in_resets_the_count()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        for (var i = 0; i < 4; i++) await TryAsync(factory, "player1", Wrong);
        Assert.Equal(HttpStatusCode.NoContent, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);

        for (var i = 0; i < 4; i++) await TryAsync(factory, "player1", Wrong);

        Assert.Equal(HttpStatusCode.NoContent, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/CromoBound.Server.Tests --filter "FullyQualifiedName~LoginProtectionTests"`
Expected: FAIL (nothing limits or locks yet: the 429 and lockout assertions fail).

- [ ] **Step 3: Write the implementation**

`src/CromoBound.Server/Accounts/LoginLockout.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Accounts;

/// <summary>Per-username lockout (spec §4.4): after <see cref="ServerOptions.LockoutFailures"/> failures in a row a name is locked
/// for <see cref="ServerOptions.LockoutMinutes"/>, whether or not the account exists. Kept in memory; a restart clears it.</summary>
internal sealed class LoginLockout(TimeProvider time, IOptions<ServerOptions> options)
{
    private sealed record Entry(int Failures, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool IsLocked(string userName) =>
        _entries.TryGetValue(UserStore.Normalize(userName), out var entry) && entry.LockedUntil is { } until && time.GetUtcNow() < until;

    public void RecordFailure(string userName) =>
        _entries.AddOrUpdate(UserStore.Normalize(userName), _ => Next(new Entry(0, null)), (_, entry) => Next(entry));

    public void Reset(string userName) => _entries.TryRemove(UserStore.Normalize(userName), out _);

    /// <summary>One more failure; a lock that has ended starts a fresh count.</summary>
    private Entry Next(Entry entry)
    {
        var now = time.GetUtcNow();
        var settings = options.Value;
        var failures = entry.LockedUntil is { } until && now >= until ? 1 : entry.Failures + 1;
        return failures >= settings.LockoutFailures ? new Entry(0, now.AddMinutes(settings.LockoutMinutes)) : new Entry(failures, null);
    }
}
```

`src/CromoBound.Server/Proxies.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CromoBound.Server;

internal static class Proxies
{
    /// <summary>Behind the reverse proxy (spec §4.4, §8): the client address and scheme come from the forwarded headers, trusted only
    /// from the configured proxies, so the login rate limit sees real client addresses.</summary>
    public static IServiceCollection AddCromoBoundProxies(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ServerOptions>>((forwarded, options) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in options.Value.KnownProxies) forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        return services;
    }
}
```

In `src/CromoBound.Server/Accounts/AccountsSetup.cs`:
- Add these usings:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
```

- Add before `return services;`:

```csharp
        services.AddSingleton<LoginLockout>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = http.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value.LoginRequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
```

- Change the method summary to: "Data protection (the keys that encrypt the cookie), cookie sign-in, the two policies with the player policy as the fallback (spec §4.1), and the login rate limit and lockout (spec §4.4)."

In `src/CromoBound.Server/Accounts/LoginEndpoints.cs`:
- Add `public const string RateLimitPolicy = "login";` after `Failure`.
- Change the sign-in mapping to `app.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);`.
- Replace `LoginAsync` with:

```csharp
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
```

Replace `src/CromoBound.Server/Program.cs` with:

```csharp
using CromoBound.Server;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.AddCromoBoundStorage(builder.Configuration);
builder.Services.AddCromoBoundAccounts(builder.Configuration);
builder.Services.AddCromoBoundProxies();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
app.Use(Privacy.NoIndexAsync);
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccounts();
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): rate-limit logins and lock out guessed usernames"
```

---

### Task 4: Admin user management

**Files:**
- Create: `src/CromoBound.Server/Accounts/AdminContracts.cs`, `src/CromoBound.Server/Accounts/AdminEndpoints.cs`
- Modify: `src/CromoBound.Server/Program.cs`
- Test: `tests/CromoBound.Server.Tests/AdminTests.cs`

**Interfaces:**
- Consumes: Task 1's `UserStore` (`FindAsync(int)`, `CreateAsync`, `SetPasswordAsync`, `SetAdminAsync`, `SetDisabledAsync`, `ActiveAdminCountAsync`, `PasswordProblem`); Task 2's `Policies.Steward`, `Sessions.UserId`, `Sessions.PrincipalFor`, `ErrorResponse`, `Roles`.
- Produces:
  - `AdminEndpoints.MapAdmin(IEndpointRouteBuilder)`, `AdminEndpoints.NotYourself` and `AdminEndpoints.LastAdmin`.
  - The records `UserSummary(int Id, string UserName, bool IsAdmin, bool Disabled)`, `CreateUserRequest(string? UserName, string? Password, bool IsAdmin)`, `PasswordRequest(string? Password)`, `RoleRequest(bool IsAdmin)` and `DisabledRequest(bool Disabled)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Server.Tests/AdminTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class AdminTests
{
    private const string NewPassword = "a-new-password-1";

    private static async Task<UserSummary> CreateAsync(HttpClient admin, string userName, bool isAdmin = false)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(userName, ServerFactory.PlayerPassword, isAdmin));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserSummary>())!;
    }

    [Fact]
    public async Task An_admin_creates_and_lists_users_without_their_password_hashes()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var created = await CreateAsync(admin, "player1");
        var list = await admin.GetAsync("/api/admin/users");
        var json = await list.Content.ReadAsStringAsync();
        var users = (await list.Content.ReadFromJsonAsync<List<UserSummary>>())!;

        Assert.Equal(new UserSummary(created.Id, "player1", false, false), created);
        Assert.Equal(new[] { ServerFactory.AdminName, "player1" }, users.Select(u => u.UserName));
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Theory]
    [InlineData("ab", "long-enough-password", "A username has")]
    [InlineData("player1", "short", "A password has")]
    [InlineData("ADMIN", "long-enough-password", "That username is taken.")]
    public async Task Bad_names_short_passwords_and_taken_names_are_refused_with_a_reason(string userName, string password, string reason)
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(userName, password, false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    [Fact]
    public async Task A_new_password_works_and_ends_the_users_sessions()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        var changed = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
        await factory.SignInAsync("player1", NewPassword);
        var old = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest("player1", ServerFactory.PlayerPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact]
    public async Task A_short_new_password_is_refused()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/password", new PasswordRequest("short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Fact]
    public async Task Promoting_demoting_and_disabling_end_the_users_sessions()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/role", new RoleRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
        var promoted = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        Assert.True((await promoted.GetFromJsonAsync<MeResponse>("/api/me"))!.CanManageUsers);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/role", new RoleRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await promoted.GetAsync("/api/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/disabled", new DisabledRequest(true))).StatusCode);
        var disabled = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest("player1", ServerFactory.PlayerPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/disabled", new DisabledRequest(false))).StatusCode);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Fact]
    public async Task An_admin_cant_demote_or_disable_themselves_and_stays_signed_in_after_changing_their_own_password()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var self = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!.Single(u => u.UserName == ServerFactory.AdminName);

        var demote = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/role", new RoleRequest(false));
        var disable = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/disabled", new DisabledRequest(true));
        var password = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Equal(AdminEndpoints.NotYourself, (await demote.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(HttpStatusCode.BadRequest, disable.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, password.StatusCode);
        Assert.True((await admin.GetFromJsonAsync<MeResponse>("/api/me"))!.CanManageUsers);
    }

    [Fact]
    public async Task Unknown_users_are_not_found()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/users/999/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_player_gets_a_bare_403_from_every_admin_endpoint_and_a_signed_out_client_a_401()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        var player = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var signedOut = factory.NewClient();
        var requests = new Func<HttpClient, Task<HttpResponseMessage>>[]
        {
            client => client.GetAsync("/api/admin/users"),
            client => client.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("someone", NewPassword, true)),
            client => client.PutAsJsonAsync("/api/admin/users/1/password", new PasswordRequest(NewPassword)),
            client => client.PutAsJsonAsync("/api/admin/users/1/role", new RoleRequest(false)),
            client => client.PutAsJsonAsync("/api/admin/users/1/disabled", new DisabledRequest(true)),
        };

        foreach (var request in requests)
        {
            var response = await request(player);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            var headers = string.Join("\n", response.Headers.Concat(response.Content.Headers).SelectMany(h => h.Value.Prepend(h.Key)));
            foreach (var secret in new[] { Roles.Seat, Roles.Steward, "seat", "steward", "admin" })
                Assert.DoesNotContain(secret, headers, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.Unauthorized, (await request(signedOut)).StatusCode);
        }
        await factory.SignInAdminAsync();
    }

    [Fact]
    public async Task Admin_endpoints_accept_only_json_bodies()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["userName"] = "player1", ["password"] = NewPassword });

        var response = await admin.PostAsync("/api/admin/users", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/CromoBound.Server.Tests --filter "FullyQualifiedName~AdminTests"`
Expected: FAIL (build errors: `UserSummary`, `CreateUserRequest`, `AdminEndpoints` don't exist).

- [ ] **Step 3: Write the implementation**

`src/CromoBound.Server/Accounts/AdminContracts.cs`:

```csharp
namespace CromoBound.Server.Accounts;

/// <summary>A user as admins see it: never a password hash, never a role name (only whether they manage users).</summary>
public sealed record UserSummary(int Id, string UserName, bool IsAdmin, bool Disabled);

public sealed record CreateUserRequest(string? UserName, string? Password, bool IsAdmin);

public sealed record PasswordRequest(string? Password);

public sealed record RoleRequest(bool IsAdmin);

public sealed record DisabledRequest(bool Disabled);
```

`src/CromoBound.Server/Accounts/AdminEndpoints.cs`:

```csharp
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
```

In `src/CromoBound.Server/Program.cs`, add `app.MapAdmin();` after `app.MapAccounts();`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including `DefaultDenyTests`, which now also covers the admin routes).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): add admin user management"
```

---

## Done criteria

- [ ] The server starts on SQLite, creates the first admin from the settings, and refuses to start without users or settings (Task 1).
- [ ] Only the login page and endpoint are public. Every other route needs a signed-in player, and signed-out requests are redirected or get a bare 401 (Task 2).
- [ ] Sign-in failures all look the same, and a changed account loses its sessions at once (Task 2).
- [ ] Logins are rate-limited per address, and five failures lock a username for 15 minutes without revealing it (Task 3).
- [ ] Admins create users, set passwords, change roles and disable accounts. They can't lock themselves out, and players get a bare 403 with no role name anywhere (Task 4).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
