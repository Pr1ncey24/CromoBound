# Server Plan H: Matches and Deployment

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Second of two plans for Phase 3 part 1 (G: foundation, done; H: matches and deployment).

**Goal:** Two signed-in players challenge each other and play a full Bo1 or Bo3 over SignalR. Each player is sent only their own view, every accepted action is saved, and a restart loses nothing. The server ships as a Docker image behind Caddy.

**Architecture:**
- **Storage:** EF Core migrations replace `EnsureCreated`. A `Matches` table holds each match's engine `MatchRecord` as JSON, written by an `IMatchStore`.
- **Card data:** loaded once at startup from `CromoBound:DataFolder` and shared read-only. A folder that can't be loaded stops the server.
- **Hub:** one SignalR hub at `/hub` behind the player policy, speaking the engine's JSON (`CromoJson.Options`, unindented). Each connection joins its user's group. A hub filter re-checks the session on every call, and an account change closes the user's live connections.
- **Lobby:** open challenges in memory, behind one gate. Accepting validates the deck, creates the engine `Match` with a cryptographic seed, saves it, and opens a `MatchHost`.
- **Matches:**
  - `MatchRegistry` holds a `MatchHost` per running match.
  - The host serializes every action behind its own lock: submit, save, then push each player their own view.
  - A failed save puts the match back to its last saved record.
  - A finished match is marked Finished and leaves memory.
- **Restart:** a hosted service reloads Running matches with `Match.Load`. A record that can't be replayed is marked Abandoned, and its players are told once.
- **Maintenance:** an in-memory switch with admin endpoints. While it's on, no new challenges or matches are made.
- **Deployment:** a multi-stage Dockerfile running as a non-root user, with the commit stamped into the engine version. `docs/server-deploy.md` covers the VPS, Caddy, the settings and backups.

**Tech Stack:**
- .NET 10 (ASP.NET Core 10.0.12, SignalR from the shared framework).
- Microsoft.EntityFrameworkCore.Sqlite 10.0.12, plus Microsoft.EntityFrameworkCore.Design 10.0.12 (design time only).
- The dotnet-ef 10.0.12 local tool.
- Tests: xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 10.0.12 and Microsoft.AspNetCore.SignalR.Client 10.0.12.

**Spec:** `docs/server.md` (sections 3, 6, 7, 8, 9, 10 row H). Builds on `docs/server-plan-g.md`.

## Global Constraints

- **Language and placement:** `net10.0` with nullable enabled (from `Directory.Build.props`). Server types are `internal`, except `Program`, the records that cross HTTP or the hub, and `IGameClient`. SignalR builds the typed client proxy at run time, so those must be public.
- **Default deny (spec §4.1):**
  - Every endpoint, the hub included, needs a signed-in user with the player role, except `GET /login` and `POST /login`.
  - Admin endpoints need the admin policy.
  - `DefaultDenyTests` enforces both for every mapped endpoint.
- **Role identifiers (spec §4.2):** no response body, header, hub message, log line or error ever contains a role identifier or role name.
- **Views (spec §6.4):**
  - A player is only ever sent their own seat's `PlayerView`, built by the engine's `ViewFor`.
  - The server never builds or edits views itself.
  - A user can't hold both seats.
- **Hub errors (spec §4.5):**
  - Expected failures come back as replies with a plain message.
  - Anything unexpected is SignalR's generic error (`EnableDetailedErrors = false`).
  - A bad call never closes the connection.
- **The engine decides:** the server passes actions to `Match.Submit` and returns the engine's own `Rejection`; it never second-guesses the rules.
- **Logging:** never log a password, a password hash or a role identifier. Never log the contents of a hidden zone.
- **Privacy (from Plan G):** nothing reachable while signed out says what the site is.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments or strings.
  - LF line endings; UTF-8 without BOM (generated migration files included).
- **dotnet:** run it with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash. The default dotnet on PATH is SDK 9. The build reports in Italian: `Avvisi` = warnings, `Errori` = errors, `Superato` = passed.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **The hub's JSON is `CromoJson.Options` without indentation** (`ServerJson.Options`): the same contract, smaller messages. Saved records use the same settings.
2. **Hub methods answer with reply records** (`HubReply`, `MatchReply`, `SubmitReply`) rather than bare values.
   - `SubmitReply` is the spec's `{ accepted, rejection }` plus an `error` for the server's own refusals (not found, unreadable, not saved).
   - `MatchReply` also carries the one-time notice that a match was abandoned at startup; that is how its players are "told" (spec §6.6).
3. **Clients send `Submit`'s action as its base type `PlayerAction`**, so its `type` name travels with it. SignalR writes arguments by their runtime type, which leaves the name out. The test client does this with `JsonSerializer.SerializeToElement<PlayerAction>`, and the Phase 4 client must do the same. An action without its type is a plain error. *Erratum (found in Task 4): System.Text.Json still writes the `type` name for a concrete action sent as a hub argument, so clients need no special handling. Only a payload with no `type` at all is refused. Sending as `PlayerAction` stays harmless.*
4. **An account change also closes the user's live hub connections**, and the hub re-checks the session on every call. A WebSocket outlives the cookie check made when it opened. This extends spec §4.3's "takes effect everywhere at once" to the hub.
5. **A record that can't be read or replayed for any reason is marked Abandoned, like a version mismatch**, so one bad row can never stop the server from starting.
6. **A failed save goes back to the last saved record kept in memory**, without reading the database again: the database may be what is failing.
7. **`GET /api/admin/maintenance` is added, and both maintenance endpoints return `{ on, runningMatches }`**, so the owner can wait for running matches to finish before a deploy.
8. **Closing challenges:**
   - `ChallengeClosed` says why: accepted, declined, cancelled, or withdrawn because one of its players started a match.
   - Starting a match withdraws every other open challenge of both players.
   - Accepting with an illegal deck leaves the challenge open.
9. **Test servers use the engine tests' small card pool** (`EngineTestDb.Create()`) unless a test loads the real data. Its fingerprint is empty, which `Match.Load` accepts as long as it matches.
10. **`ServerFactory` clears only its own database's connection pool**, not every pool. The global clear could close other tests' connections, which run in parallel; it is the suspect in a flaky startup failure seen in Plan G.
11. **No test runs Docker** (it isn't installed on the development machine). Task 6 checks the publish command the Dockerfile runs, including the version stamp. The owner builds the image on the VPS.

## Review Focus

1. **A player challenging themselves** (in any letter case) would hold both seats and receive both views. It is refused. Pinned in Task 2 (`A_challenge_needs_another_enabled_player`, row `"ALICE"`).
2. **Unreadable hub input:** a null deck, an unknown format, a challenge id that isn't a GUID, a null action, or an action without its type. Each gets a plain error, and the connection stays open for the next call. Pinned in Task 2 (`Unreadable_input_is_a_plain_error_and_the_connection_stays_open`) and Task 4 (`A_match_the_player_isnt_in_reads_as_not_found_and_an_empty_action_is_refused`, `An_action_sent_without_its_type_is_a_plain_error_and_the_connection_stays_open`).
3. **A disabled or changed account with an open WebSocket** stops receiving and sending at once, not at the next page load. Pinned in Task 2 (`A_changed_account_closes_its_live_connections_at_once`, `A_call_after_the_session_changed_elsewhere_is_refused_and_closes_the_connection`).
4. **A save that fails** never leaves the live match ahead of the saved record, and no view is pushed for the lost action. Pinned in Task 4 (`A_failed_save_takes_the_match_back_to_its_last_saved_state`).
5. **One unreadable saved match** never stops the server from starting. Pinned in Task 5 (`An_unreadable_match_record_is_abandoned_and_the_server_still_starts`).

---

## File Structure

```
.config/dotnet-tools.json                 dotnet-ef 10.0.12 local tool
.dockerignore, Dockerfile
docs/server-deploy.md                     VPS, Caddy, settings, backups, deploy checklist
docs/server.md                            (modify: layout, configuration, deployment)
src/CromoBound.Server/
  CromoBound.Server.csproj                (modify: EF Core Design, design time only)
  Program.cs                              (modify: matches, hub, maintenance endpoints)
  ServerJson.cs                           CromoJson.Options without indentation
  Storage/MatchEntity.cs                  the match row and its status
  Storage/CromoDbContext.cs               (modify: Matches)
  Storage/DatabaseSetup.cs                (modify: migrations instead of EnsureCreated)
  Storage/DesignTimeDbContextFactory.cs   for dotnet-ef
  Storage/Migrations/                     generated: the Initial migration and the model snapshot
  Accounts/Sessions.cs                    (modify: IsCurrentAsync)
  Accounts/UserStore.cs                   (modify: an account change closes live connections)
  Accounts/AccountsSetup.cs               (modify: LiveConnections)
  Accounts/LiveConnections.cs             each user's open hub connections
  Accounts/AdminContracts.cs              (modify: maintenance records)
  Matches/MatchesSetup.cs                 card data, store, lobby, registry, maintenance, SignalR
  Matches/MatchStore.cs                   IMatchStore and the SQLite store
  Matches/MatchStartup.cs                 checks card data and reloads running matches at startup
  Matches/MatchSeat.cs                    a seat's user
  Matches/Lobby.cs                        open challenges and starting matches
  Matches/MatchRegistry.cs                running matches by id and by user
  Matches/MatchHost.cs                    one running match behind its lock
  Matches/Maintenance.cs                  the switch
  Matches/MaintenanceEndpoints.cs         /api/admin/maintenance
  Hubs/GameContracts.cs                   replies and notices
  Hubs/IGameClient.cs                     what the server pushes
  Hubs/GameHub.cs                         the hub
  Hubs/SessionFilter.cs                   re-checks the session on every call
tests/CromoBound.Server.Tests/
  CromoBound.Server.Tests.csproj          (modify: SignalR client; links the engine tests' EngineTestDb, RepoPaths, Bot)
  ServerFactory.cs                        (modify: test cards, own pool, database helpers, session cookie)
  StartupTests.cs                         (modify)
  MatchStoreTests.cs, Decks.cs, GameClient.cs, HubTests.cs, ChallengeTests.cs,
  TwoPlayers.cs, MatchStartTests.cs, MaintenanceTests.cs, TestMatchStore.cs, MatchPlayTests.cs, RestartTests.cs
```

---

### Task 1: Migrations, the Matches table and the card data

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `src/CromoBound.Server/ServerJson.cs`, `Storage/MatchEntity.cs`, `Storage/DesignTimeDbContextFactory.cs`, `Storage/Migrations/*` (generated)
- Create: `src/CromoBound.Server/Matches/MatchesSetup.cs`, `Matches/MatchStore.cs`, `Matches/MatchStartup.cs`
- Modify: `src/CromoBound.Server/CromoBound.Server.csproj`, `Program.cs`, `Storage/CromoDbContext.cs`, `Storage/DatabaseSetup.cs`
- Modify: `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, `ServerFactory.cs`, `StartupTests.cs`
- Create: `tests/CromoBound.Server.Tests/MatchStoreTests.cs`

**Interfaces:**
- Consumes (Plan G):
  - `ServerOptions.DataFolder` and `ServerOptions.Section`.
  - `CromoDbContext`, `DatabaseSetup`, `StorageSetup.AddCromoBoundStorage`.
  - In the tests, `ServerFactory`.
- Consumes (engine):
  - `CardRepository.Load(string) : CardDatabase`.
  - `MatchRecord`, `Match.Create`, `Match.Load`.
  - `CromoJson.Options`.
- Produces:
  - `ServerJson.Options`.
  - `MatchStatus { Running, Finished, Abandoned }`.
  - `MatchEntity` (`Id`, `Seat0UserId`, `Seat1UserId`, `RecordJson`, `Status`, `CreatedAt`, `UpdatedAt`).
  - `CromoDbContext.Matches`.
  - `IMatchStore`, with these members:
    - `CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record)`;
    - `SaveAsync(Guid id, MatchRecord record, MatchStatus status)`;
    - `SetStatusAsync(Guid id, MatchStatus status)`;
    - `RunningAsync() : Task<IReadOnlyList<MatchEntity>>`.
  - `MatchStore` (implements it), with `static Json(MatchRecord) : string` and `static Read(string) : MatchRecord`.
  - `MatchesSetup.AddCromoBoundMatches(IServiceCollection)`.
  - `MatchStartup` (hosted service).
  - In the tests:
    - `ServerFactory.TestCards`;
    - the constructor parameter `loadCards`;
    - `WithDbAsync(Func<CromoDbContext, Task>)`;
    - `static NewDatabasePath()` and `static DeleteDatabase(string)`.
  - The test project compiles the engine tests' `EngineTestDb.cs` (with `TestDecks`) and `RepoPaths.cs`.

- [ ] **Step 1: Add the packages, the tool and the linked engine test files**

Create `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "10.0.12",
      "commands": [
        "dotnet-ef"
      ],
      "rollForward": false
    }
  }
}
```

In `src/CromoBound.Server/CromoBound.Server.csproj`, add to the package `ItemGroup`:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

In `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, add before the `ProjectReference` group:

```xml
  <ItemGroup>
    <!-- The engine tests' card pool, legal decks and repository paths, compiled in here too: they're internal to the engine tests. -->
    <Compile Include="..\CromoBound.Engine.Tests\EngineTestDb.cs" Link="Engine\EngineTestDb.cs" />
    <Compile Include="..\CromoBound.Engine.Tests\RepoPaths.cs" Link="Engine\RepoPaths.cs" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

Replace `tests/CromoBound.Server.Tests/ServerFactory.cs` with:

```csharp
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
```

In `tests/CromoBound.Server.Tests/StartupTests.cs`:
- Add the usings `using CromoBound.Data;` and `using CromoBound.Engine.Tests;`.
- Remove `using Microsoft.Data.Sqlite;`.
- Replace the last test with:

```csharp
    [Fact]
    public async Task A_database_with_users_keeps_them_and_ignores_the_first_admin_settings()
    {
        var path = ServerFactory.NewDatabasePath();
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
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task The_database_is_built_by_the_migrations()
    {
        using var factory = new ServerFactory();

        await factory.WithDbAsync(async db =>
        {
            Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_Initial", StringComparison.Ordinal));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(0, await db.Matches.CountAsync());
        });
    }

    [Fact]
    public void The_card_data_is_loaded_from_the_data_folder_at_startup()
    {
        using var factory = new ServerFactory(new() { ["CromoBound:DataFolder"] = RepoPaths.Data }, loadCards: true);

        var cards = factory.Services.GetRequiredService<CardDatabase>();

        Assert.NotEmpty(cards.Cards);
        Assert.NotEqual("", cards.Fingerprint);
    }

    [Fact]
    public void A_card_data_folder_that_cant_be_loaded_stops_the_server_and_is_named()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"cromobound-no-cards-{Guid.NewGuid():N}");
        using var factory = new ServerFactory(new() { ["CromoBound:DataFolder"] = missing }, loadCards: true);

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("CromoBound:DataFolder", error.ToString());
    }
```

Create `tests/CromoBound.Server.Tests/MatchStoreTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Tests;
using CromoBound.Models.Json;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchStoreTests
{
    private static Match NewMatch(ulong seed = 7) => Match.Create(TestDecks.Setup(MatchFormat.Bo1, seed), ServerFactory.TestCards).Match!;

    private static async Task<(IMatchStore Store, int Alice, int Bob)> StoreAsync(ServerFactory factory)
    {
        var alice = await factory.AddUserAsync("alice");
        var bob = await factory.AddUserAsync("bob");
        return (factory.Services.GetRequiredService<IMatchStore>(), alice.Id, bob.Id);
    }

    [Fact]
    public async Task A_new_match_is_running_and_a_save_replaces_its_record()
    {
        using var factory = new ServerFactory();
        var (store, alice, bob) = await StoreAsync(factory);
        var id = Guid.NewGuid();

        await store.CreateAsync(id, alice, bob, NewMatch(7).ToRecord());
        var created = Assert.Single(await store.RunningAsync());
        await store.SaveAsync(id, NewMatch(8).ToRecord(), MatchStatus.Running);
        var saved = Assert.Single(await store.RunningAsync());

        Assert.Equal((id, alice, bob, MatchStatus.Running), (created.Id, created.Seat0UserId, created.Seat1UserId, created.Status));
        Assert.Equal(7UL, MatchStore.Read(created.RecordJson).Setup.Seed);
        Assert.Equal(8UL, MatchStore.Read(saved.RecordJson).Setup.Seed);
        Assert.True(saved.UpdatedAt >= created.UpdatedAt);
        Assert.Equal(created.CreatedAt, saved.CreatedAt);
    }

    [Theory]
    [InlineData("Finished")]
    [InlineData("Abandoned")]
    public async Task Finished_and_abandoned_matches_arent_running_and_keep_their_record(string status)
    {
        using var factory = new ServerFactory();
        var (store, alice, bob) = await StoreAsync(factory);
        var id = Guid.NewGuid();
        await store.CreateAsync(id, alice, bob, NewMatch(7).ToRecord());

        await store.SetStatusAsync(id, Enum.Parse<MatchStatus>(status));

        Assert.Empty(await store.RunningAsync());
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            Assert.Equal(Enum.Parse<MatchStatus>(status), row.Status);
            Assert.Equal(7UL, MatchStore.Read(row.RecordJson).Setup.Seed);
        });
    }

    [Fact]
    public async Task Saving_a_match_that_isnt_there_fails()
    {
        using var factory = new ServerFactory();
        var (store, _, _) = await StoreAsync(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(Guid.NewGuid(), NewMatch().ToRecord(), MatchStatus.Running));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetStatusAsync(Guid.NewGuid(), MatchStatus.Abandoned));
    }

    [Fact]
    public void A_saved_record_reads_back_and_replays_to_the_same_match()
    {
        var match = NewMatch();
        var first = match.Pending!.Players[0];
        Assert.True(match.Submit(first, new ChoosePlayOrder(true)).Accepted);

        var loaded = Match.Load(MatchStore.Read(MatchStore.Json(match.ToRecord())), ServerFactory.TestCards);

        foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
            Assert.Equal(CromoJson.Serialize(match.ViewFor(seat)), CromoJson.Serialize(loaded.ViewFor(seat)));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`MatchStatus`, `IMatchStore`, `MatchStore`, `CromoDbContext.Matches` don't exist).

- [ ] **Step 4: Write the implementation**

Create `src/CromoBound.Server/ServerJson.cs`:

```csharp
using System.Text.Json;
using CromoBound.Models.Json;

namespace CromoBound.Server;

/// <summary>The engine's JSON settings (<see cref="CromoJson.Options"/>) without indentation: what the hub speaks and how saved match
/// records are written. Same names, same polymorphic type names, same enums as strings.</summary>
internal static class ServerJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(CromoJson.Options) { WriteIndented = false };
        options.MakeReadOnly();
        return options;
    }
}
```

Create `src/CromoBound.Server/Storage/MatchEntity.cs`:

```csharp
namespace CromoBound.Server.Storage;

internal enum MatchStatus { Running, Finished, Abandoned }

/// <summary>A match (spec §6.6): its two players by seat and the engine's saved record as JSON, rewritten after every accepted
/// action. A finished or abandoned match keeps its record.</summary>
internal sealed class MatchEntity
{
    public Guid Id { get; set; }
    public int Seat0UserId { get; set; }
    public int Seat1UserId { get; set; }
    public required string RecordJson { get; set; }
    public MatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Replace `src/CromoBound.Server/Storage/CromoDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Storage;

/// <summary>The server's SQLite database. Its schema comes from the migrations in <c>Storage/Migrations</c>.</summary>
internal sealed class CromoDbContext(DbContextOptions<CromoDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<UserEntity>();
        user.ToTable("Users");
        user.Property(u => u.UserName).HasMaxLength(24);
        user.Property(u => u.NormalizedUserName).HasMaxLength(24);
        user.HasIndex(u => u.NormalizedUserName).IsUnique();

        var match = model.Entity<MatchEntity>();
        match.ToTable("Matches");
        match.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
        match.HasIndex(m => m.Status);
        match.HasOne<UserEntity>().WithMany().HasForeignKey(m => m.Seat0UserId).OnDelete(DeleteBehavior.Restrict);
        match.HasOne<UserEntity>().WithMany().HasForeignKey(m => m.Seat1UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

Create `src/CromoBound.Server/Storage/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CromoBound.Server.Storage;

/// <summary>Used only by <c>dotnet ef</c> to build migrations, without starting the server. It never opens the file.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CromoDbContext>
{
    public CromoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CromoDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
```

In `src/CromoBound.Server/Storage/DatabaseSetup.cs`:
- Replace `await db.Database.EnsureCreatedAsync(cancellationToken);` with `await db.Database.MigrateAsync(cancellationToken);`.
- Change the summary's first line to "Runs before the server accepts requests: brings the database up to the latest migration and, while there are no users, creates the first admin from ...".

The rest of the file stays as it is. A database made by Plan G's `EnsureCreated` has no migration history, so `MigrateAsync` would fail on it. There is no deployed database yet; delete any local `cromobound.db` made before this task.

Create `src/CromoBound.Server/Matches/MatchStore.cs`:

```csharp
using System.Text.Json;
using CromoBound.Engine.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Matches;

/// <summary>Where matches are kept (spec §6.6). An interface so tests can make saves fail.</summary>
internal interface IMatchStore
{
    Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record);

    /// <summary>Replaces the record and the status. Throws when the match isn't there or the write fails.</summary>
    Task SaveAsync(Guid id, MatchRecord record, MatchStatus status);

    /// <summary>Changes the status only, keeping the record. Throws when the match isn't there.</summary>
    Task SetStatusAsync(Guid id, MatchStatus status);

    /// <summary>Every Running match, oldest first.</summary>
    Task<IReadOnlyList<MatchEntity>> RunningAsync();
}

/// <summary>The Matches table. Each call uses its own database context, so match hosts can save from any thread.</summary>
internal sealed class MatchStore(IServiceScopeFactory scopes, TimeProvider time) : IMatchStore
{
    public static string Json(MatchRecord record) => JsonSerializer.Serialize(record, ServerJson.Options);

    public static MatchRecord Read(string json) =>
        JsonSerializer.Deserialize<MatchRecord>(json, ServerJson.Options) ?? throw new JsonException("A match record was null.");

    public async Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CromoDbContext>();
        var now = time.GetUtcNow().UtcDateTime;
        db.Matches.Add(new MatchEntity
        {
            Id = id, Seat0UserId = seat0UserId, Seat1UserId = seat1UserId, RecordJson = Json(record),
            Status = MatchStatus.Running, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(Guid id, MatchRecord record, MatchStatus status)
    {
        using var scope = scopes.CreateScope();
        var json = Json(record);
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await Rows(scope, id).ExecuteUpdateAsync(set => set
            .SetProperty(m => m.RecordJson, json)
            .SetProperty(m => m.Status, status)
            .SetProperty(m => m.UpdatedAt, now));
        if (changed != 1) throw new InvalidOperationException($"Match {id} isn't in the database.");
    }

    public async Task SetStatusAsync(Guid id, MatchStatus status)
    {
        using var scope = scopes.CreateScope();
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await Rows(scope, id).ExecuteUpdateAsync(set => set
            .SetProperty(m => m.Status, status)
            .SetProperty(m => m.UpdatedAt, now));
        if (changed != 1) throw new InvalidOperationException($"Match {id} isn't in the database.");
    }

    public async Task<IReadOnlyList<MatchEntity>> RunningAsync()
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatus.Running).OrderBy(m => m.CreatedAt).ToListAsync();
    }

    private static IQueryable<MatchEntity> Rows(IServiceScope scope, Guid id) =>
        scope.ServiceProvider.GetRequiredService<CromoDbContext>().Matches.Where(m => m.Id == id);
}
```

Create `src/CromoBound.Server/Matches/MatchStartup.cs`:

```csharp
using CromoBound.Data;

namespace CromoBound.Server.Matches;

/// <summary>Runs at startup, after the database is ready. Taking the card data loads it, so a data folder that can't be loaded stops
/// the server before it accepts requests.</summary>
internal sealed class MatchStartup(CardDatabase cards, ILogger<MatchStartup> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Loaded {Count} cards.", cards.Cards.Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Create `src/CromoBound.Server/Matches/MatchesSetup.cs`:

```csharp
using CromoBound.Data;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Matches;

internal static class MatchesSetup
{
    /// <summary>The card data (loaded once, shared read-only), the match store and the startup work. Call after
    /// <c>AddCromoBoundStorage</c>, so the database is ready before <see cref="MatchStartup"/> runs.</summary>
    public static IServiceCollection AddCromoBoundMatches(this IServiceCollection services)
    {
        services.AddSingleton(provider => LoadCards(provider.GetRequiredService<IOptions<ServerOptions>>().Value.DataFolder));
        services.AddSingleton<IMatchStore, MatchStore>();
        services.AddHostedService<MatchStartup>();
        return services;
    }

    private static CardDatabase LoadCards(string folder)
    {
        try
        {
            return CardRepository.Load(folder);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"{ServerOptions.Section}:{nameof(ServerOptions.DataFolder)} must name the card data folder: '{folder}' can't be loaded ({ex.Message})", ex);
        }
    }
}
```

In `src/CromoBound.Server/Program.cs`:
- Add `using CromoBound.Server.Matches;`.
- Add `builder.Services.AddCromoBoundMatches();` after `builder.Services.AddCromoBoundProxies();`.

- [ ] **Step 5: Generate the initial migration**

Run, from the repository root:

```bash
export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && dotnet tool restore && dotnet ef migrations add Initial --project src/CromoBound.Server --output-dir Storage/Migrations --namespace CromoBound.Server.Storage.Migrations
```

Expected: three files in `src/CromoBound.Server/Storage/Migrations/`:
- `<timestamp>_Initial.cs`;
- `<timestamp>_Initial.Designer.cs`;
- `CromoDbContextModelSnapshot.cs`.

The Initial migration creates `Users` (with its unique `NormalizedUserName` index) and `Matches` (with the `Status` index and the two foreign keys).

dotnet-ef writes CRLF line endings and may write a byte-order mark. Normalize the files:

```bash
for f in src/CromoBound.Server/Storage/Migrations/*.cs; do sed -i 's/\r$//; 1s/^\xEF\xBB\xBF//' "$f"; done
```

Don't edit the generated code otherwise. If it contains an em or en dash, stop and report it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 7: Commit**

```bash
git add .config src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): add migrations, the matches table and card data loading"
```

---

### Task 2: The hub, live sessions and challenges

**Files:**
- Create: `src/CromoBound.Server/Accounts/LiveConnections.cs`
- Create: `src/CromoBound.Server/Hubs/GameContracts.cs`, `Hubs/IGameClient.cs`, `Hubs/GameHub.cs`, `Hubs/SessionFilter.cs`
- Create: `src/CromoBound.Server/Matches/MatchSeat.cs`, `Matches/Lobby.cs`
- Modify: `src/CromoBound.Server/Accounts/Sessions.cs`, `Accounts/UserStore.cs`, `Accounts/AccountsSetup.cs`, `Matches/MatchesSetup.cs`, `Program.cs`
- Modify: `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, `ServerFactory.cs`
- Create: `tests/CromoBound.Server.Tests/Decks.cs`, `GameClient.cs`, `HubTests.cs`, `ChallengeTests.cs`

**Interfaces:**
- Consumes:
  - From Task 1: `ServerJson.Options`, `MatchesSetup`, `CromoDbContext`, `ServerFactory`.
  - From Plan G:
    - `Sessions.UserId(ClaimsPrincipal) : int?` and `Sessions.StampClaim`;
    - `UserStore.FindAsync(string)` and `UserStore.UserNameProblem(string?)`;
    - `Policies.Seat`.
  - From the engine and data: `DeckValidator.Validate(Deck, CardDatabase) : DeckReport`, `MatchFormat`.
- Produces:
  - `Sessions.IsCurrentAsync(ClaimsPrincipal, CromoDbContext) : Task<bool>`.
  - `LiveConnections`: `Opened(string connectionId, int userId, Action abort)`, `Closed(string connectionId)`, `EndAll(int userId)`.
  - `SessionFilter.Ended`.
  - `MatchSeat(int UserId, string UserName)`.
  - Hub records:
    - `HubReply(Guid? Id, string? Error, IReadOnlyList<DeckIssue>? DeckIssues = null)`, with `Ok(Guid)`, `Fail(string)` and `Done`;
    - `ChallengeEnd { Accepted, Declined, Cancelled, Withdrawn }`;
    - `ChallengeNotice(Guid ChallengeId, string From, MatchFormat Format)`;
    - `ChallengeClosedNotice(Guid ChallengeId, ChallengeEnd Reason)`.
  - `IGameClient.ChallengeReceived` and `IGameClient.ChallengeClosed`.
  - `GameHub.UserGroup(int) : string`, and the hub methods `Challenge(string? opponent, MatchFormat format, Deck? deck)`, `DeclineChallenge(Guid)` and `CancelChallenge(Guid)`.
  - `Lobby`:
    - the constants `NoSuchPlayer`, `NotYourself`, `AlreadyChallenging`, `NoSuchChallenge`, `IllegalDeck`, `NoDeck`;
    - `ChallengeAsync(MatchSeat, string?, MatchFormat, Deck?)`, `DeclineAsync(MatchSeat, Guid)`, `CancelAsync(MatchSeat, Guid)`.
  - In the tests:
    - `ServerFactory.SessionCookieAsync(userName, password) : Task<string>`;
    - `Decks.First`, `Decks.Second`, `Decks.Illegal`;
    - `GameClient`, with these members:
      - `NewPlayerAsync`, `ConnectAsync` and `ConnectWithCookieAsync`;
      - `Connection` and `Closed`;
      - `ChallengeAsync`, `DeclineAsync` and `CancelAsync`;
      - `All<T>()` and `WaitForAsync<T>(Func<T, bool>?)`.

- [ ] **Step 1: Add the SignalR client to the tests**

In `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, add to the package `ItemGroup`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.12" />
```

- [ ] **Step 2: Write the failing tests**

In `tests/CromoBound.Server.Tests/ServerFactory.cs`, add this method after `SignInAdminAsync`:

```csharp
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
```

Create `tests/CromoBound.Server.Tests/Decks.cs`:

```csharp
using CromoBound.Engine.Tests;
using CromoBound.Models.Cards;

namespace CromoBound.Server.Tests;

/// <summary>Decks over <see cref="ServerFactory.TestCards"/>: two legal ones with different battlefields, and one with a single battlefield.</summary>
internal static class Decks
{
    public static Deck First => TestDecks.Jinx("bf-a", "bf-b", "bf-c");
    public static Deck Second => TestDecks.Jinx("bf-d", "bf-e", "bf-f");
    public static Deck Illegal => TestDecks.Jinx("bf-a");
}
```

Create `tests/CromoBound.Server.Tests/GameClient.cs`:

```csharp
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>A player's hub connection over the test server's WebSockets, signed in with a session cookie. It keeps every notice the
/// server pushes, in arrival order.</summary>
internal sealed class GameClient : IAsyncDisposable
{
    private readonly List<object> _received = [];
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GameClient(HubConnection connection)
    {
        Connection = connection;
        Record<ChallengeNotice>(nameof(IGameClient.ChallengeReceived));
        Record<ChallengeClosedNotice>(nameof(IGameClient.ChallengeClosed));
        connection.Closed += _ =>
        {
            _closed.TrySetResult();
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    /// <summary>Completes when the server closes the connection.</summary>
    public Task Closed => _closed.Task;

    /// <summary>Adds a user with <see cref="ServerFactory.PlayerPassword"/> and connects as them.</summary>
    public static async Task<GameClient> NewPlayerAsync(ServerFactory factory, string userName)
    {
        await factory.AddUserAsync(userName);
        return await ConnectAsync(factory, userName);
    }

    public static async Task<GameClient> ConnectAsync(ServerFactory factory, string userName, string password = ServerFactory.PlayerPassword) =>
        await ConnectWithCookieAsync(factory, await factory.SessionCookieAsync(userName, password));

    /// <summary>Connects with the given cookie header value; throws when the server refuses the connection.</summary>
    public static async Task<GameClient> ConnectWithCookieAsync(ServerFactory factory, string cookie)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("https://localhost/hub", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancel) =>
                {
                    var socket = factory.Server.CreateWebSocketClient();
                    socket.ConfigureRequest = request => request.Headers.Cookie = cookie;
                    return await socket.ConnectAsync(context.Uri, cancel);
                };
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions = ServerJson.Options)
            .Build();
        var client = new GameClient(connection);
        await connection.StartAsync();
        return client;
    }

    public Task<HubReply> ChallengeAsync(string opponent, Deck? deck, MatchFormat format = MatchFormat.Bo1) =>
        Connection.InvokeAsync<HubReply>("Challenge", opponent, format, deck);

    public Task<HubReply> DeclineAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("DeclineChallenge", challengeId);

    public Task<HubReply> CancelAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("CancelChallenge", challengeId);

    /// <summary>Every notice of this type received so far.</summary>
    public IReadOnlyList<T> All<T>()
    {
        lock (_received) return [.. _received.OfType<T>()];
    }

    /// <summary>The first notice of this type (matching <paramref name="match"/>, if given), waiting up to ten seconds for it.</summary>
    public async Task<T> WaitForAsync<T>(Func<T, bool>? match = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            lock (_received)
                foreach (var notice in _received.OfType<T>())
                    if (match?.Invoke(notice) ?? true) return notice;
            await _arrived.WaitAsync(timeout.Token);
        }
    }

    private void Record<T>(string method) => Connection.On<T>(method, notice =>
    {
        lock (_received) _received.Add(notice!);
        _arrived.Release();
    });

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
```

Create `tests/CromoBound.Server.Tests/HubTests.cs`:

```csharp
using System.Net;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Tests;

/// <summary>The hub's door: signed-out callers get nothing, and a changed account loses its live connections at once.</summary>
public class HubTests
{
    [Fact]
    public async Task Signed_out_the_hub_is_a_bare_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();
        using var page = new HttpRequestMessage(HttpMethod.Get, "/hub");
        page.Headers.Accept.ParseAdd("text/html");

        var responses = new[] { await client.PostAsync("/hub/negotiate?negotiateVersion=1", null), await client.SendAsync(page) };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task A_forged_session_cant_open_a_connection()
    {
        using var factory = new ServerFactory();

        await Assert.ThrowsAnyAsync<Exception>(() => GameClient.ConnectWithCookieAsync(factory, "__Host-session=forged"));
    }

    [Fact]
    public async Task A_signed_in_player_connects()
    {
        using var factory = new ServerFactory();

        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");

        Assert.Equal(HubConnectionState.Connected, alice.Connection.State);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("password")]
    [InlineData("promote")]
    public async Task A_changed_account_closes_its_live_connections_at_once(string change)
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("alice");
        var cookie = await factory.SessionCookieAsync("alice", ServerFactory.PlayerPassword);
        await using var alice = await GameClient.ConnectWithCookieAsync(factory, cookie);
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        await factory.WithStoreAsync(async users =>
        {
            var user = (await users.FindAsync("alice"))!;
            await (change switch
            {
                "disable" => users.SetDisabledAsync(user, true),
                "password" => users.SetPasswordAsync(user, "another-password-1"),
                _ => users.SetAdminAsync(user, true),
            });
        });

        await alice.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HubConnectionState.Connected, bob.Connection.State);
        await Assert.ThrowsAnyAsync<Exception>(() => GameClient.ConnectWithCookieAsync(factory, cookie));
    }

    [Fact]
    public async Task A_call_after_the_session_changed_elsewhere_is_refused_and_closes_the_connection()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.WithDbAsync(db => db.Users.Where(u => u.UserName == "alice")
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.SecurityStamp, "changed-elsewhere")));

        await Assert.ThrowsAnyAsync<Exception>(() => alice.ChallengeAsync("bob", Decks.First));

        await alice.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(bob.All<ChallengeNotice>());
    }
}
```

The last test changes the stamp behind the store's back, so nothing closes the connection in advance. The filter must catch the stale session on the call itself. Whether the client sees the refusal message or a closed connection depends on which arrives first, so the test accepts either.

Create `tests/CromoBound.Server.Tests/ChallengeTests.cs`:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Server.Tests;

public class ChallengeTests
{
    [Fact]
    public async Task A_challenge_reaches_the_opponent_whatever_the_case_of_their_name()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        var reply = await alice.ChallengeAsync("BOB", Decks.First, MatchFormat.Bo3);

        Assert.Null(reply.Error);
        var notice = await bob.WaitForAsync<ChallengeNotice>();
        Assert.Equal(new ChallengeNotice(reply.Id!.Value, "alice", MatchFormat.Bo3), notice);
        Assert.Empty(alice.All<ChallengeNotice>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Declining_or_cancelling_closes_the_challenge_for_both(bool decline)
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        var reply = decline ? await bob.DeclineAsync(id) : await alice.CancelAsync(id);

        Assert.Null(reply.Error);
        var closed = new ChallengeClosedNotice(id, decline ? ChallengeEnd.Declined : ChallengeEnd.Cancelled);
        Assert.Equal(closed, await alice.WaitForAsync<ChallengeClosedNotice>());
        Assert.Equal(closed, await bob.WaitForAsync<ChallengeClosedNotice>());
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.DeclineAsync(id)).Error);
    }

    [Fact]
    public async Task Only_the_challenged_player_declines_and_only_the_challenger_cancels()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        Assert.Equal(Lobby.NoSuchChallenge, (await alice.DeclineAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.CancelAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await carol.DeclineAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.DeclineAsync(Guid.NewGuid())).Error);
        Assert.Empty(bob.All<ChallengeClosedNotice>());

        Assert.Null((await bob.DeclineAsync(id)).Error);
    }

    [Theory]
    [InlineData("nobody", Lobby.NoSuchPlayer)]
    [InlineData("carol", Lobby.NoSuchPlayer)]
    [InlineData("not a name", Lobby.NoSuchPlayer)]
    [InlineData("", Lobby.NoSuchPlayer)]
    [InlineData("ALICE", Lobby.NotYourself)]
    public async Task A_challenge_needs_another_enabled_player(string opponent, string error)
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await factory.AddUserAsync("carol");
        await factory.WithStoreAsync(async users => await users.SetDisabledAsync((await users.FindAsync("carol"))!, true));

        var reply = await alice.ChallengeAsync(opponent, Decks.First);

        Assert.Null(reply.Id);
        Assert.Equal(error, reply.Error);
        Assert.Empty(alice.All<ChallengeNotice>());
    }

    [Fact]
    public async Task A_challenger_has_one_open_challenge_at_a_time()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var first = await alice.ChallengeAsync("bob", Decks.First);

        Assert.Equal(Lobby.AlreadyChallenging, (await alice.ChallengeAsync("carol", Decks.First)).Error);
        Assert.Empty(carol.All<ChallengeNotice>());
        Assert.Null((await bob.ChallengeAsync("alice", Decks.Second)).Error);

        await alice.CancelAsync(first.Id!.Value);
        Assert.Null((await alice.ChallengeAsync("carol", Decks.First)).Error);
    }

    [Fact]
    public async Task An_illegal_deck_is_refused_with_its_issues()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        var reply = await alice.ChallengeAsync("bob", Decks.Illegal);

        Assert.Equal(Lobby.IllegalDeck, reply.Error);
        Assert.Contains(reply.DeckIssues!, issue => issue.Code == DeckIssueCode.BattlefieldCount);
        Assert.Empty(bob.All<ChallengeNotice>());
    }

    [Fact]
    public async Task Unreadable_input_is_a_plain_error_and_the_connection_stays_open()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        Assert.Equal(Lobby.NoDeck, (await alice.ChallengeAsync("bob", null)).Error);
        await Assert.ThrowsAsync<HubException>(() => alice.Connection.InvokeAsync<HubReply>("Challenge", "bob", "Bo5", Decks.First));
        await Assert.ThrowsAsync<HubException>(() => alice.Connection.InvokeAsync<HubReply>("CancelChallenge", "not-a-guid"));

        Assert.Equal(HubConnectionState.Connected, alice.Connection.State);
        Assert.Null((await alice.ChallengeAsync("bob", Decks.First)).Error);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`CromoBound.Server.Hubs`, `Lobby` and the hub records don't exist).

- [ ] **Step 4: Write the implementation**

In `src/CromoBound.Server/Accounts/Sessions.cs`, replace `ValidateAsync` with these two methods:

```csharp
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
```

Create `src/CromoBound.Server/Accounts/LiveConnections.cs`:

```csharp
using System.Collections.Concurrent;

namespace CromoBound.Server.Accounts;

/// <summary>Each user's open hub connections. A hub connection is checked once when it opens, so a change that ends a user's sessions
/// (spec §4.3) must also close their live connections, or a disabled player would keep receiving.</summary>
internal sealed class LiveConnections
{
    private readonly ConcurrentDictionary<string, (int UserId, Action Abort)> _open = new();

    public void Opened(string connectionId, int userId, Action abort) => _open[connectionId] = (userId, abort);

    public void Closed(string connectionId) => _open.TryRemove(connectionId, out _);

    /// <summary>Closes every open connection of the user.</summary>
    public void EndAll(int userId)
    {
        foreach (var (_, connection) in _open)
            if (connection.UserId == userId) connection.Abort();
    }
}
```

In `src/CromoBound.Server/Accounts/UserStore.cs`:
- Change the class declaration to `internal sealed partial class UserStore(CromoDbContext db, IPasswordHasher<UserEntity> hasher, TimeProvider time, LiveConnections connections)`.
- Extend its summary's last sentence to: "A change to the password, the role or the disabled flag replaces the security stamp, which ends the user's sessions and closes their live hub connections (spec §4.3)."
- Replace `ChangedAsync` with:

```csharp
    private async Task ChangedAsync(UserEntity user, CancellationToken cancel)
    {
        user.SecurityStamp = NewStamp();
        await db.SaveChangesAsync(cancel);
        connections.EndAll(user.Id);
    }
```

In `src/CromoBound.Server/Accounts/AccountsSetup.cs`, add `services.AddSingleton<LiveConnections>();` next to `services.AddSingleton<LoginLockout>();`.

Create `src/CromoBound.Server/Matches/MatchSeat.cs`:

```csharp
namespace CromoBound.Server.Matches;

/// <summary>The user in a seat, or making a challenge.</summary>
internal sealed record MatchSeat(int UserId, string UserName);
```

Create `src/CromoBound.Server/Hubs/GameContracts.cs`:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Matches;

namespace CromoBound.Server.Hubs;

/// <summary>The answer to a challenge call: the challenge's or match's id, or why not (with the deck's problems for an illegal deck).</summary>
public sealed record HubReply(Guid? Id, string? Error, IReadOnlyList<DeckIssue>? DeckIssues = null)
{
    public static HubReply Done { get; } = new(null, null);

    public static HubReply Ok(Guid id) => new(id, null);

    public static HubReply Fail(string error) => new(null, error);
}

public enum ChallengeEnd
{
    Accepted,
    Declined,
    Cancelled,

    /// <summary>One of its players started another match.</summary>
    Withdrawn,
}

/// <summary>Someone challenged the receiving player.</summary>
public sealed record ChallengeNotice(Guid ChallengeId, string From, MatchFormat Format);

/// <summary>A challenge the receiving player made or received is closed.</summary>
public sealed record ChallengeClosedNotice(Guid ChallengeId, ChallengeEnd Reason);
```

Create `src/CromoBound.Server/Hubs/IGameClient.cs`:

```csharp
namespace CromoBound.Server.Hubs;

/// <summary>What the server pushes to a player's connections (spec §6.4). Public: SignalR builds the typed proxy at run time.</summary>
public interface IGameClient
{
    Task ChallengeReceived(ChallengeNotice challenge);

    Task ChallengeClosed(ChallengeClosedNotice closed);
}
```

Create `src/CromoBound.Server/Hubs/SessionFilter.cs`:

```csharp
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Hubs;

/// <summary>Before every hub call: the caller's session must still be current. A stale one is refused and its connection closed.</summary>
internal sealed class SessionFilter : IHubFilter
{
    public const string Ended = "Your session has ended.";

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var db = invocation.ServiceProvider.GetRequiredService<CromoDbContext>();
        if (await Sessions.IsCurrentAsync(invocation.Context.User!, db)) return await next(invocation);
        invocation.Context.Abort();
        throw new HubException(Ended);
    }
}
```

Create `src/CromoBound.Server/Matches/Lobby.cs`:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>Open challenges (spec §6.2), kept in memory: a restart drops them. One gate serializes every change, so the rules can't be
/// raced.</summary>
internal sealed class Lobby(CardDatabase cards, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)
{
    public const string NoSuchPlayer = "There is no such player.";
    public const string NotYourself = "You can't challenge yourself.";
    public const string AlreadyChallenging = "You already have an open challenge.";
    public const string NoSuchChallenge = "There is no such challenge.";
    public const string IllegalDeck = "That deck isn't legal.";
    public const string NoDeck = "Send a deck.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OpenChallenge> _open = [];

    private sealed record OpenChallenge(Guid Id, MatchSeat From, MatchSeat To, MatchFormat Format, Deck Deck);

    /// <summary>The opponent must be another enabled user (found in any letter case); the deck must be legal; a challenger has one
    /// open challenge at a time.</summary>
    public async Task<HubReply> ChallengeAsync(MatchSeat me, string? opponent, MatchFormat format, Deck? deck)
    {
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (_open.Values.Any(c => c.From.UserId == me.UserId)) return HubReply.Fail(AlreadyChallenging);
            if (await FindPlayerAsync(opponent) is not { } them) return HubReply.Fail(NoSuchPlayer);
            if (them.UserId == me.UserId) return HubReply.Fail(NotYourself);
            var report = DeckValidator.Validate(deck, cards);
            if (!report.IsLegal) return new HubReply(null, IllegalDeck, report.Issues);
            var challenge = new OpenChallenge(Guid.NewGuid(), me, them, format, deck);
            _open.Add(challenge.Id, challenge);
            await hub.Clients.Group(GameHub.UserGroup(them.UserId)).ChallengeReceived(new ChallengeNotice(challenge.Id, me.UserName, format));
            return HubReply.Ok(challenge.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<HubReply> DeclineAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.To.UserId == me.UserId, ChallengeEnd.Declined);

    public Task<HubReply> CancelAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.From.UserId == me.UserId, ChallengeEnd.Cancelled);

    /// <summary>A challenge the caller may not close reads as missing.</summary>
    private async Task<HubReply> CloseAsync(Guid challengeId, Func<OpenChallenge, bool> mayClose, ChallengeEnd reason)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_open.TryGetValue(challengeId, out var challenge) || !mayClose(challenge)) return HubReply.Fail(NoSuchChallenge);
            await RemoveAsync(challenge, reason);
            return HubReply.Done;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RemoveAsync(OpenChallenge challenge, ChallengeEnd reason)
    {
        _open.Remove(challenge.Id);
        await hub.Clients.Groups([GameHub.UserGroup(challenge.From.UserId), GameHub.UserGroup(challenge.To.UserId)])
            .ChallengeClosed(new ChallengeClosedNotice(challenge.Id, reason));
    }

    /// <summary>An enabled user by name in any letter case; a missing, disabled or malformed name is no one.</summary>
    private async Task<MatchSeat?> FindPlayerAsync(string? userName)
    {
        if (UserStore.UserNameProblem(userName) is not null) return null;
        using var scope = scopes.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserStore>().FindAsync(userName!);
        return user is { Disabled: false } ? new MatchSeat(user.Id, user.UserName) : null;
    }
}
```

Create `src/CromoBound.Server/Hubs/GameHub.cs`:

```csharp
using System.Globalization;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Hubs;

/// <summary>The one hub (spec §6), at /hub behind the player policy. Every connection joins its user's group, so all of a user's tabs
/// get their notices. Methods answer with replies; anything unexpected is SignalR's generic error.</summary>
internal sealed class GameHub(Lobby lobby, LiveConnections connections, CromoDbContext db) : Hub<IGameClient>
{
    public static string UserGroup(int userId) => "user-" + userId.ToString(CultureInfo.InvariantCulture);

    private MatchSeat Me => new(Sessions.UserId(Context.User!)!.Value, Context.User!.Identity!.Name!);

    /// <summary>The connection is tracked before its session is checked again, so an account change made in between still closes it.</summary>
    public override async Task OnConnectedAsync()
    {
        var me = Me;
        connections.Opened(Context.ConnectionId, me.UserId, Context.Abort);
        if (!await Sessions.IsCurrentAsync(Context.User!, db))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(me.UserId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Closed(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public Task<HubReply> Challenge(string? opponent, MatchFormat format, Deck? deck) => lobby.ChallengeAsync(Me, opponent, format, deck);

    public Task<HubReply> DeclineChallenge(Guid challengeId) => lobby.DeclineAsync(Me, challengeId);

    public Task<HubReply> CancelChallenge(Guid challengeId) => lobby.CancelAsync(Me, challengeId);
}
```

In `src/CromoBound.Server/Matches/MatchesSetup.cs`:
- Add `using CromoBound.Server.Hubs;` and `using Microsoft.AspNetCore.SignalR;`.
- Change the summary to "The card data (loaded once, shared read-only), the match store, the lobby, the hub and the startup work. ...".
- Add before `return services;`:

```csharp
        services.AddSingleton<Lobby>();
        services.AddSignalR(hub =>
        {
            hub.EnableDetailedErrors = false;
            hub.AddFilter<SessionFilter>();
        }).AddJsonProtocol(json => json.PayloadSerializerOptions = ServerJson.Options);
```

In `src/CromoBound.Server/Program.cs`:
- Add `using CromoBound.Server.Hubs;`.
- After `app.MapAdmin();`, add:

```csharp
app.MapHub<GameHub>("/hub").RequireAuthorization(Policies.Seat);
```

`Sessions.ChallengeAsync` already treats `/hub` as a non-page path, so signed-out hub requests get a bare 401. `DefaultDenyTests` covers the hub's two endpoints (`/hub` and `/hub/negotiate`).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including `DefaultDenyTests` over the hub endpoints).

- [ ] **Step 6: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): add the game hub with live sessions and challenges"
```

---
### Task 3: Starting matches and the maintenance switch

**Files:**
- Create: `src/CromoBound.Server/Matches/MatchHost.cs`, `Matches/MatchRegistry.cs`, `Matches/Maintenance.cs`, `Matches/MaintenanceEndpoints.cs`
- Modify: `src/CromoBound.Server/Hubs/GameContracts.cs`, `Hubs/IGameClient.cs`, `Hubs/GameHub.cs`, `Matches/Lobby.cs`, `Matches/MatchesSetup.cs`, `Accounts/AdminContracts.cs`, `Program.cs`
- Modify: `tests/CromoBound.Server.Tests/GameClient.cs`
- Create: `tests/CromoBound.Server.Tests/TwoPlayers.cs`, `MatchStartTests.cs`, `MaintenanceTests.cs`

**Interfaces:**
- Consumes:
  - From Task 1: `IMatchStore.CreateAsync`.
  - From Task 2: `Lobby`, `GameHub`, `IGameClient`, `HubReply`, `ChallengeEnd`, `MatchSeat`, `GameClient`, `Decks`.
  - From the engine:
    - `Match.Create(MatchSetup, CardDatabase) : MatchCreateResult` (`Match`, `Reports`);
    - `MatchSetup(MatchFormat, Deck, Deck, ulong)`;
    - `Match.ViewFor(PlayerId) : PlayerView` and `Match.ToRecord()`.
  - From Plan G: `ErrorResponse`, `Policies.Steward`.
- Produces:
  - Hub records:
    - `MatchStartedNotice(Guid MatchId, string Opponent, PlayerId Seat)`;
    - `MatchViewNotice(Guid MatchId, PlayerView View)`;
    - `MatchEndReason { Finished, Abandoned }`;
    - `MatchEndedNotice(Guid MatchId, MatchEndReason Reason, IReadOnlyList<int> GameWins, string? Winner)`;
    - `MatchReply(Guid? MatchId, PlayerView? View, MatchEndedNotice? Ended)`, with `None`.
  - `IGameClient.MatchStarted` and `IGameClient.View`.
  - The hub methods `AcceptChallenge(Guid, Deck?)` and `GetMatch()`.
  - `MatchHost`: `Id`, `Match`, `Seats`, `SeatOf(int) : PlayerId?`, `StartAsync()`, `ViewAsync(PlayerId)`.
  - `MatchRegistry`: `Count`, `IsPlaying(int)`, `Find(Guid)`, `Open(Guid, Match, IReadOnlyList<MatchSeat>)`, `CurrentAsync(int)`.
  - `Lobby.AcceptAsync(MatchSeat, Guid, Deck?)`, plus the constants `InMaintenance`, `YouArePlaying` and `TheyArePlaying`.
  - `Maintenance.On`.
  - `MaintenanceEndpoints.SayOn`.
  - `MaintenanceRequest(bool? On)` and `MaintenanceStatus(bool On, int RunningMatches)`.
  - In the tests:
    - `GameClient.AcceptAsync` and `GameClient.GetMatchAsync`;
    - `TwoPlayers`: `StartAsync(factory, first, second)`, `First`, `Second`, `this[PlayerId]`, `MatchId`, `Host`, and `static AsReceived(PlayerView)`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Server.Tests/GameClient.cs`:
- Add the two notices to the constructor, after the `ChallengeClosedNotice` line:

```csharp
        Record<MatchStartedNotice>(nameof(IGameClient.MatchStarted));
        Record<MatchViewNotice>(nameof(IGameClient.View));
```

- Add these methods after `CancelAsync`:

```csharp
    public Task<HubReply> AcceptAsync(Guid challengeId, Deck? deck) => Connection.InvokeAsync<HubReply>("AcceptChallenge", challengeId, deck);

    public Task<MatchReply> GetMatchAsync() => Connection.InvokeAsync<MatchReply>("GetMatch");
```

Create `tests/CromoBound.Server.Tests/TwoPlayers.cs`:

```csharp
using System.Text.Json;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Json;
using CromoBound.Server.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>Two players connected to the hub with a running Bo1 between them: the challenger in seat 0, the other in seat 1.</summary>
internal sealed class TwoPlayers : IAsyncDisposable
{
    private readonly ServerFactory _factory;

    private TwoPlayers(ServerFactory factory, GameClient first, GameClient second, Guid matchId)
    {
        _factory = factory;
        First = first;
        Second = second;
        MatchId = matchId;
    }

    /// <summary>The challenger, in seat 0.</summary>
    public GameClient First { get; }

    public GameClient Second { get; }

    public Guid MatchId { get; }

    public GameClient this[PlayerId seat] => seat.Index == 0 ? First : Second;

    /// <summary>The server's host of the match (while it runs).</summary>
    public MatchHost Host => _factory.Services.GetRequiredService<MatchRegistry>().Find(MatchId)!;

    /// <summary>Adds the two users, connects them, and starts a Bo1 with a challenge and its acceptance.</summary>
    public static async Task<TwoPlayers> StartAsync(ServerFactory factory, string first = "alice", string second = "bob")
    {
        var challenger = await GameClient.NewPlayerAsync(factory, first);
        var opponent = await GameClient.NewPlayerAsync(factory, second);
        var challenge = await challenger.ChallengeAsync(second, Decks.First);
        Assert.Null(challenge.Error);
        var accepted = await opponent.AcceptAsync(challenge.Id!.Value, Decks.Second);
        Assert.Null(accepted.Error);
        return new TwoPlayers(factory, challenger, opponent, accepted.Id!.Value);
    }

    /// <summary>A view as a player receives it: written by the server and read back by the client, where empty lists read back as
    /// null. Compare views a client received with this, not with the engine's view directly.</summary>
    public static string AsReceived(PlayerView view) =>
        CromoJson.Serialize(JsonSerializer.Deserialize<PlayerView>(JsonSerializer.Serialize(view, ServerJson.Options), ServerJson.Options));

    public async ValueTask DisposeAsync()
    {
        await First.DisposeAsync();
        await Second.DisposeAsync();
    }
}
```

Create `tests/CromoBound.Server.Tests/MatchStartTests.cs`:

```csharp
using CromoBound.Data;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchStartTests
{
    [Fact]
    public async Task Accepting_starts_a_running_match_with_the_challenger_in_seat_0()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        Assert.Equal(new MatchStartedNotice(players.MatchId, "bob", new PlayerId(0)), await players.First.WaitForAsync<MatchStartedNotice>());
        Assert.Equal(new MatchStartedNotice(players.MatchId, "alice", new PlayerId(1)), await players.Second.WaitForAsync<MatchStartedNotice>());
        foreach (var client in new[] { players.First, players.Second })
            Assert.Equal(ChallengeEnd.Accepted, (await client.WaitForAsync<ChallengeClosedNotice>()).Reason);
        Assert.Equal(1, factory.Services.GetRequiredService<MatchRegistry>().Count);
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            var ids = await db.Users.ToDictionaryAsync(u => u.UserName, u => u.Id);
            Assert.Equal((players.MatchId, MatchStatus.Running, ids["alice"], ids["bob"]), (row.Id, row.Status, row.Seat0UserId, row.Seat1UserId));
        });
    }

    [Fact]
    public async Task Each_player_is_sent_their_own_view_and_gets_it_back_from_GetMatch()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
        {
            var expected = TwoPlayers.AsReceived(players.Host.Match.ViewFor(seat));
            var pushed = await players[seat].WaitForAsync<MatchViewNotice>();
            var current = await players[seat].GetMatchAsync();

            Assert.Equal(players.MatchId, pushed.MatchId);
            Assert.Equal(expected, CromoJson.Serialize(pushed.View));
            Assert.Equal(players.MatchId, current.MatchId);
            Assert.Equal(expected, CromoJson.Serialize(current.View));
        }
        Assert.Equal(MatchReply.None, await carol.GetMatchAsync());
    }

    [Fact]
    public async Task An_illegal_deck_doesnt_accept_and_the_challenge_stays_open()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        var refused = await bob.AcceptAsync(id, Decks.Illegal);

        Assert.Equal(Lobby.IllegalDeck, refused.Error);
        Assert.Contains(refused.DeckIssues!, issue => issue.Code == DeckIssueCode.BattlefieldCount);
        Assert.Equal(Lobby.NoDeck, (await bob.AcceptAsync(id, null)).Error);
        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
        Assert.Null((await bob.AcceptAsync(id, Decks.Second)).Error);
    }

    [Fact]
    public async Task Only_the_challenged_player_can_accept()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        Assert.Equal(Lobby.NoSuchChallenge, (await alice.AcceptAsync(id, Decks.Second)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await carol.AcceptAsync(id, Decks.Second)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.AcceptAsync(Guid.NewGuid(), Decks.Second)).Error);

        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
    }

    [Fact]
    public async Task Players_in_a_match_cant_challenge_or_be_challenged()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        Assert.Equal(Lobby.YouArePlaying, (await players.First.ChallengeAsync("carol", Decks.First)).Error);
        Assert.Equal(Lobby.TheyArePlaying, (await carol.ChallengeAsync("bob", Decks.First)).Error);

        Assert.Empty(carol.All<ChallengeNotice>());
    }

    [Fact]
    public async Task Starting_a_match_withdraws_the_players_other_challenges()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var dave = await GameClient.NewPlayerAsync(factory, "dave");
        var aliceToBob = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;
        var carolToAlice = (await carol.ChallengeAsync("alice", Decks.First)).Id!.Value;
        var bobToDave = (await bob.ChallengeAsync("dave", Decks.Second)).Id!.Value;
        var daveToCarol = (await dave.ChallengeAsync("carol", Decks.First)).Id!.Value;

        Assert.Null((await bob.AcceptAsync(aliceToBob, Decks.Second)).Error);

        Assert.Equal(ChallengeEnd.Withdrawn, (await carol.WaitForAsync<ChallengeClosedNotice>(c => c.ChallengeId == carolToAlice)).Reason);
        Assert.Equal(ChallengeEnd.Withdrawn, (await dave.WaitForAsync<ChallengeClosedNotice>(c => c.ChallengeId == bobToDave)).Reason);
        Assert.Equal(Lobby.NoSuchChallenge, (await dave.AcceptAsync(bobToDave, Decks.First)).Error);
        Assert.Null((await carol.AcceptAsync(daveToCarol, Decks.Second)).Error);
    }
}
```

Create `tests/CromoBound.Server.Tests/MaintenanceTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;

namespace CromoBound.Server.Tests;

public class MaintenanceTests
{
    private const string Route = "/api/admin/maintenance";

    private static Task<HttpResponseMessage> SwitchAsync(HttpClient client, bool? on) => client.PostAsJsonAsync(Route, new MaintenanceRequest(on));

    [Fact]
    public async Task Maintenance_stops_new_challenges_and_matches_but_not_running_ones()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var dave = await GameClient.NewPlayerAsync(factory, "dave");
        var open = (await dave.ChallengeAsync("carol", Decks.First)).Id!.Value;
        var admin = await factory.SignInAdminAsync();

        var on = await SwitchAsync(admin, true);

        Assert.Equal(new MaintenanceStatus(true, 1), await on.Content.ReadFromJsonAsync<MaintenanceStatus>());
        Assert.Equal(Lobby.InMaintenance, (await carol.AcceptAsync(open, Decks.Second)).Error);
        Assert.Equal(Lobby.InMaintenance, (await carol.ChallengeAsync("dave", Decks.Second)).Error);
        Assert.Equal(players.MatchId, (await players.First.GetMatchAsync()).MatchId);
        Assert.Equal(new MaintenanceStatus(true, 1), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));

        await SwitchAsync(admin, false);

        Assert.Null((await carol.AcceptAsync(open, Decks.Second)).Error);
        Assert.Equal(new MaintenanceStatus(false, 2), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));
    }

    [Fact]
    public async Task Only_admins_see_or_switch_maintenance_and_a_switch_must_say_which()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        var player = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var admin = await factory.SignInAdminAsync();

        foreach (var response in new[] { await player.GetAsync(Route), await SwitchAsync(player, true) })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await SwitchAsync(factory.NewClient(), true)).StatusCode);
        var vague = await SwitchAsync(admin, null);
        Assert.Equal(HttpStatusCode.BadRequest, vague.StatusCode);
        Assert.Equal(MaintenanceEndpoints.SayOn, (await vague.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(new MaintenanceStatus(false, 0), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`MatchStartedNotice`, `MatchRegistry`, `MaintenanceStatus` and the rest don't exist).

- [ ] **Step 3: Write the implementation**

Append to `src/CromoBound.Server/Hubs/GameContracts.cs`, after its other records, and add `using CromoBound.Engine.State;` and `using CromoBound.Engine.Views;` at the top:

```csharp
/// <summary>A match started; the receiving player is in <see cref="Seat"/> and plays <see cref="Opponent"/>.</summary>
public sealed record MatchStartedNotice(Guid MatchId, string Opponent, PlayerId Seat);

/// <summary>The receiving player's own view of the match, sent after every accepted action.</summary>
public sealed record MatchViewNotice(Guid MatchId, PlayerView View);

public enum MatchEndReason
{
    Finished,

    /// <summary>The server was updated (or the saved match couldn't be replayed), so the match can't go on.</summary>
    Abandoned,
}

/// <summary>A match is over: the game wins by seat, and the winner's name (none when abandoned).</summary>
public sealed record MatchEndedNotice(Guid MatchId, MatchEndReason Reason, IReadOnlyList<int> GameWins, string? Winner);

/// <summary>GetMatch's answer: the player's running match and their view of it; or, once, the notice of a match of theirs that was
/// abandoned when the server restarted; or nothing.</summary>
public sealed record MatchReply(Guid? MatchId, PlayerView? View, MatchEndedNotice? Ended)
{
    public static MatchReply None { get; } = new(null, null, null);
}
```

In `src/CromoBound.Server/Hubs/IGameClient.cs`, add after `ChallengeClosed`:

```csharp

    Task MatchStarted(MatchStartedNotice started);

    /// <summary>The receiving player's own view, never the other seat's.</summary>
    Task View(MatchViewNotice view);
```

Create `src/CromoBound.Server/Matches/MatchHost.cs`:

```csharp
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>One running match (spec §6.6): the engine's match and its two seats. A lock serializes everything that reads or changes
/// the match, so each player's view is built from a settled state.</summary>
internal sealed class MatchHost(Guid id, Match match, IReadOnlyList<MatchSeat> seats, IHubContext<GameHub, IGameClient> hub)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Guid Id => id;

    public Match Match { get; } = match;

    public IReadOnlyList<MatchSeat> Seats => seats;

    public PlayerId? SeatOf(int userId)
    {
        for (var i = 0; i < seats.Count; i++)
            if (seats[i].UserId == userId) return new PlayerId(i);
        return null;
    }

    /// <summary>Tells each player the match started, then sends each their first view.</summary>
    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            for (var i = 0; i < seats.Count; i++)
                await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId))
                    .MatchStarted(new MatchStartedNotice(id, seats[1 - i].UserName, new PlayerId(i)));
            await PushViewsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PlayerView> ViewAsync(PlayerId seat)
    {
        await _lock.WaitAsync();
        try
        {
            return Match.ViewFor(seat);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Each player gets their own seat's view, and only that.</summary>
    private async Task PushViewsAsync()
    {
        for (var i = 0; i < seats.Count; i++)
            await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId)).View(new MatchViewNotice(id, Match.ViewFor(new PlayerId(i))));
    }
}
```

Create `src/CromoBound.Server/Matches/MatchRegistry.cs`:

```csharp
using System.Collections.Concurrent;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>The running matches (spec §6.6), by id and by player. A player is in at most one.</summary>
internal sealed class MatchRegistry(IHubContext<GameHub, IGameClient> hub)
{
    private readonly ConcurrentDictionary<Guid, MatchHost> _hosts = new();
    private readonly ConcurrentDictionary<int, MatchHost> _byUser = new();

    public int Count => _hosts.Count;

    public bool IsPlaying(int userId) => _byUser.ContainsKey(userId);

    public MatchHost? Find(Guid matchId) => _hosts.GetValueOrDefault(matchId);

    /// <summary>Holds a match that was just created.</summary>
    public MatchHost Open(Guid id, Match match, IReadOnlyList<MatchSeat> seats)
    {
        var host = new MatchHost(id, match, seats, hub);
        _hosts[id] = host;
        foreach (var seat in seats) _byUser[seat.UserId] = host;
        return host;
    }

    /// <summary>The user's running match and their view of it, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat), null)
            : MatchReply.None;
}
```

Create `src/CromoBound.Server/Matches/Maintenance.cs`:

```csharp
namespace CromoBound.Server.Matches;

/// <summary>The maintenance switch (spec §6.7): in memory, off at startup. While it's on, no challenge is made or accepted; running
/// matches go on, so they can finish before a deploy.</summary>
internal sealed class Maintenance
{
    private volatile bool _on;

    public bool On
    {
        get => _on;
        set => _on = value;
    }
}
```

Replace `src/CromoBound.Server/Matches/Lobby.cs` with:

```csharp
using System.Security.Cryptography;
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>Open challenges (spec §6.2), kept in memory: a restart drops them. One gate serializes every change and every match start,
/// so the rules can't be raced. Players in a match can't make or receive challenges, and starting a match withdraws every other open
/// challenge of its players, so no open challenge ever involves a player who is in a match.</summary>
internal sealed class Lobby(CardDatabase cards, IMatchStore store, MatchRegistry matches, Maintenance maintenance,
    IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)
{
    public const string InMaintenance = "The server is in maintenance.";
    public const string NoSuchPlayer = "There is no such player.";
    public const string NotYourself = "You can't challenge yourself.";
    public const string YouArePlaying = "You are already in a match.";
    public const string TheyArePlaying = "That player is in a match.";
    public const string AlreadyChallenging = "You already have an open challenge.";
    public const string NoSuchChallenge = "There is no such challenge.";
    public const string IllegalDeck = "That deck isn't legal.";
    public const string NoDeck = "Send a deck.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OpenChallenge> _open = [];

    private sealed record OpenChallenge(Guid Id, MatchSeat From, MatchSeat To, MatchFormat Format, Deck Deck);

    /// <summary>The opponent must be another enabled user (found in any letter case) who isn't in a match; the deck must be legal; a
    /// challenger has one open challenge at a time.</summary>
    public async Task<HubReply> ChallengeAsync(MatchSeat me, string? opponent, MatchFormat format, Deck? deck)
    {
        if (maintenance.On) return HubReply.Fail(InMaintenance);
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (matches.IsPlaying(me.UserId)) return HubReply.Fail(YouArePlaying);
            if (_open.Values.Any(c => c.From.UserId == me.UserId)) return HubReply.Fail(AlreadyChallenging);
            if (await FindPlayerAsync(opponent) is not { } them) return HubReply.Fail(NoSuchPlayer);
            if (them.UserId == me.UserId) return HubReply.Fail(NotYourself);
            if (matches.IsPlaying(them.UserId)) return HubReply.Fail(TheyArePlaying);
            var report = DeckValidator.Validate(deck, cards);
            if (!report.IsLegal) return new HubReply(null, IllegalDeck, report.Issues);
            var challenge = new OpenChallenge(Guid.NewGuid(), me, them, format, deck);
            _open.Add(challenge.Id, challenge);
            await hub.Clients.Group(GameHub.UserGroup(them.UserId)).ChallengeReceived(new ChallengeNotice(challenge.Id, me.UserName, format));
            return HubReply.Ok(challenge.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Only the challenged player accepts, with a legal deck; an illegal one leaves the challenge open. The match is created
    /// with a seed from a cryptographic generator and the challenger in seat 0 (the engine's roll-off still decides who plays first),
    /// saved, and opened. Every other open challenge of either player is withdrawn.</summary>
    public async Task<HubReply> AcceptAsync(MatchSeat me, Guid challengeId, Deck? deck)
    {
        if (maintenance.On) return HubReply.Fail(InMaintenance);
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (!_open.TryGetValue(challengeId, out var challenge) || challenge.To.UserId != me.UserId) return HubReply.Fail(NoSuchChallenge);
            var created = Match.Create(new MatchSetup(challenge.Format, challenge.Deck, deck, NewSeed()), cards);
            if (created.Match is not { } match) return new HubReply(null, IllegalDeck, created.Reports[1].Issues);
            var id = Guid.NewGuid();
            await store.CreateAsync(id, challenge.From.UserId, challenge.To.UserId, match.ToRecord());
            var host = matches.Open(id, match, [challenge.From, challenge.To]);
            foreach (var other in _open.Values.Where(c => Involves(c, challenge.From) || Involves(c, challenge.To)).ToList())
                await RemoveAsync(other, other.Id == challengeId ? ChallengeEnd.Accepted : ChallengeEnd.Withdrawn);
            await host.StartAsync();
            return HubReply.Ok(id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<HubReply> DeclineAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.To.UserId == me.UserId, ChallengeEnd.Declined);

    public Task<HubReply> CancelAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.From.UserId == me.UserId, ChallengeEnd.Cancelled);

    /// <summary>A challenge the caller may not close reads as missing.</summary>
    private async Task<HubReply> CloseAsync(Guid challengeId, Func<OpenChallenge, bool> mayClose, ChallengeEnd reason)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_open.TryGetValue(challengeId, out var challenge) || !mayClose(challenge)) return HubReply.Fail(NoSuchChallenge);
            await RemoveAsync(challenge, reason);
            return HubReply.Done;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RemoveAsync(OpenChallenge challenge, ChallengeEnd reason)
    {
        _open.Remove(challenge.Id);
        await hub.Clients.Groups([GameHub.UserGroup(challenge.From.UserId), GameHub.UserGroup(challenge.To.UserId)])
            .ChallengeClosed(new ChallengeClosedNotice(challenge.Id, reason));
    }

    private static bool Involves(OpenChallenge challenge, MatchSeat player) =>
        challenge.From.UserId == player.UserId || challenge.To.UserId == player.UserId;

    private static ulong NewSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));

    /// <summary>An enabled user by name in any letter case; a missing, disabled or malformed name is no one.</summary>
    private async Task<MatchSeat?> FindPlayerAsync(string? userName)
    {
        if (UserStore.UserNameProblem(userName) is not null) return null;
        using var scope = scopes.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserStore>().FindAsync(userName!);
        return user is { Disabled: false } ? new MatchSeat(user.Id, user.UserName) : null;
    }
}
```

`created.Reports[1]` is the accepting player's deck. The challenger's deck was checked when the challenge was made, against the same card data.

Replace `src/CromoBound.Server/Hubs/GameHub.cs` with:

```csharp
using System.Globalization;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Hubs;

/// <summary>The one hub (spec §6), at /hub behind the player policy. Every connection joins its user's group, so all of a user's tabs
/// get their notices. Methods answer with replies; anything unexpected is SignalR's generic error.</summary>
internal sealed class GameHub(Lobby lobby, MatchRegistry matches, LiveConnections connections, CromoDbContext db) : Hub<IGameClient>
{
    public static string UserGroup(int userId) => "user-" + userId.ToString(CultureInfo.InvariantCulture);

    private MatchSeat Me => new(Sessions.UserId(Context.User!)!.Value, Context.User!.Identity!.Name!);

    /// <summary>The connection is tracked before its session is checked again, so an account change made in between still closes it.</summary>
    public override async Task OnConnectedAsync()
    {
        var me = Me;
        connections.Opened(Context.ConnectionId, me.UserId, Context.Abort);
        if (!await Sessions.IsCurrentAsync(Context.User!, db))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(me.UserId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Closed(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public Task<HubReply> Challenge(string? opponent, MatchFormat format, Deck? deck) => lobby.ChallengeAsync(Me, opponent, format, deck);

    public Task<HubReply> AcceptChallenge(Guid challengeId, Deck? deck) => lobby.AcceptAsync(Me, challengeId, deck);

    public Task<HubReply> DeclineChallenge(Guid challengeId) => lobby.DeclineAsync(Me, challengeId);

    public Task<HubReply> CancelChallenge(Guid challengeId) => lobby.CancelAsync(Me, challengeId);

    /// <summary>The caller's running match and view, on connect and on reconnect.</summary>
    public Task<MatchReply> GetMatch() => matches.CurrentAsync(Me.UserId);
}
```

Append to `src/CromoBound.Server/Accounts/AdminContracts.cs`:

```csharp

public sealed record MaintenanceRequest(bool? On);

/// <summary>Whether maintenance is on, and how many matches are still running.</summary>
public sealed record MaintenanceStatus(bool On, int RunningMatches);
```

Create `src/CromoBound.Server/Matches/MaintenanceEndpoints.cs`:

```csharp
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Matches;

/// <summary>The maintenance switch for admins (spec §5.2, §6.7). Both routes answer with the switch and the number of running
/// matches, so a deploy can wait for them to finish.</summary>
internal static class MaintenanceEndpoints
{
    public const string SayOn = "Say whether maintenance is on.";

    public static void MapMaintenance(this IEndpointRouteBuilder app)
    {
        var maintenance = app.MapGroup("/api/admin/maintenance").RequireAuthorization(Policies.Steward);
        maintenance.MapGet("", (Maintenance state, MatchRegistry matches) => Status(state, matches));
        maintenance.MapPost("", Switch);
    }

    /// <summary>The body must say which.</summary>
    private static IResult Switch(MaintenanceRequest request, Maintenance state, MatchRegistry matches, ILogger<Maintenance> log)
    {
        if (request.On is not { } on) return Results.BadRequest(new ErrorResponse(SayOn));
        state.On = on;
        log.LogInformation("Maintenance is {State}.", on ? "on" : "off");
        return Results.Ok(Status(state, matches));
    }

    private static MaintenanceStatus Status(Maintenance state, MatchRegistry matches) => new(state.On, matches.Count);
}
```

In `src/CromoBound.Server/Matches/MatchesSetup.cs`:
- Add `services.AddSingleton<MatchRegistry>();` and `services.AddSingleton<Maintenance>();` before `services.AddSingleton<Lobby>();`.
- Change the summary to "The card data (loaded once, shared read-only), the match store, the running matches, the maintenance switch, the lobby, the hub and the startup work. ...".

In `src/CromoBound.Server/Program.cs`, add `app.MapMaintenance();` after `app.MapAdmin();`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including `DefaultDenyTests` over the maintenance routes).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): start matches from challenges and add the maintenance switch"
```

---

### Task 4: Playing: actions, saves, views and the end of a match

**Files:**
- Modify: `src/CromoBound.Server/Matches/MatchHost.cs` (replace), `Matches/MatchRegistry.cs` (replace), `Matches/Lobby.cs` (one line)
- Modify: `src/CromoBound.Server/Hubs/GameContracts.cs`, `Hubs/IGameClient.cs`, `Hubs/GameHub.cs`
- Modify: `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, `GameClient.cs`, `TwoPlayers.cs`
- Create: `tests/CromoBound.Server.Tests/TestMatchStore.cs`, `MatchPlayTests.cs`

**Interfaces:**
- Consumes:
  - From Task 1: `IMatchStore.SaveAsync`, `MatchStore`, `MatchStatus`.
  - From Task 3: `MatchHost`, `MatchRegistry`, `MatchEndedNotice`, `MatchEndReason`, `TwoPlayers`.
  - From the engine:
    - `Match.Submit(PlayerId, PlayerAction) : SubmitResult` (`Accepted`, `Rejection`);
    - `Match.Stage`, `MatchStage.Over`, `Match.Result` (`GameWins`, `Winner`);
    - `Match.Load(MatchRecord, CardDatabase)`.
  - The engine tests' `Bot` (`Choose(Match) : PlayerAction`).
- Produces:
  - `SubmitReply(bool Accepted, Rejection? Rejection, string? Error)`.
  - `IGameClient.MatchEnded`.
  - The hub method `Submit(Guid matchId, PlayerAction? action)`.
  - `MatchHost`: the constant `SaveFailed`, and `SubmitAsync(PlayerId, PlayerAction) : Task<SubmitReply>`.
  - `MatchRegistry`:
    - the constants `NotFound` and `UnreadableAction`;
    - `Open(Guid id, Match match, MatchRecord saved, IReadOnlyList<MatchSeat> seats)`;
    - `SubmitAsync(int userId, Guid matchId, PlayerAction? action)`.
  - In the tests:
    - `GameClient.SubmitAsync(Guid, PlayerAction)` and `GameClient.SubmitRawAsync(Guid, object?)`;
    - `TwoPlayers.PlayAsync(int actions = 2000)`;
    - `TestMatchStore` (`Register`, `Saves`, `FailSaves`).

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Server.Tests/CromoBound.Server.Tests.csproj`, add to the `Compile Include` group from Task 1:

```xml
    <Compile Include="..\CromoBound.Engine.Tests\Bot.cs" Link="Engine\Bot.cs" />
```

In `tests/CromoBound.Server.Tests/GameClient.cs`:
- Add `using System.Text.Json;` and `using CromoBound.Engine.Actions;`.
- Add this line to the constructor, after the `MatchViewNotice` line:

```csharp
        Record<MatchEndedNotice>(nameof(IGameClient.MatchEnded));
```

- Add these methods after `GetMatchAsync`:

```csharp
    /// <summary>Sends the action as a <see cref="PlayerAction"/>, so its type name travels with it.</summary>
    public Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action) =>
        Connection.InvokeAsync<SubmitReply>("Submit", matchId, JsonSerializer.SerializeToElement(action, ServerJson.Options));

    /// <summary>Sends the value as it is: a concrete action this way goes without its type name.</summary>
    public Task<SubmitReply> SubmitRawAsync(Guid matchId, object? action) => Connection.InvokeAsync<SubmitReply>("Submit", matchId, action);
```

In `tests/CromoBound.Server.Tests/TwoPlayers.cs`:
- Add `using CromoBound.Engine.Tests;`.
- Add after the `_factory` field:

```csharp
    private readonly Bot[] _bots = [new(), new()];
```

- Add after `StartAsync`:

```csharp
    /// <summary>The engine tests' scripted Bot plays both seats through the hub, each action sent by the deciding player's own connection,
    /// until the match ends or <paramref name="actions"/> actions are sent. Every action must be accepted.</summary>
    public async Task PlayAsync(int actions = 2000)
    {
        var matches = _factory.Services.GetRequiredService<MatchRegistry>();
        for (var i = 0; i < actions && matches.Find(MatchId) is { } host; i++)
        {
            var match = host.Match;
            var seat = match.Pending!.Players[0];
            var reply = await this[seat].SubmitAsync(MatchId, _bots[seat.Index].Choose(match));
            Assert.True(reply.Accepted, reply.Rejection?.Message ?? reply.Error);
        }
    }
```

The test reads the host's match between actions. It sends one action at a time, so the match is settled whenever the test reads it.

Create `tests/CromoBound.Server.Tests/TestMatchStore.cs`:

```csharp
using CromoBound.Engine.Matches;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The real match store, counting saves and failing them on demand.</summary>
internal sealed class TestMatchStore(MatchStore inner) : IMatchStore
{
    private int _saves;
    private volatile bool _failSaves;

    /// <summary>Puts one of these in place of the server's store: <c>new ServerFactory(services: TestMatchStore.Register)</c>.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<MatchStore>();
        services.AddSingleton<TestMatchStore>();
        services.AddSingleton<IMatchStore>(provider => provider.GetRequiredService<TestMatchStore>());
    }

    /// <summary>Saves that went through.</summary>
    public int Saves => Volatile.Read(ref _saves);

    public bool FailSaves
    {
        get => _failSaves;
        set => _failSaves = value;
    }

    public Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record) => inner.CreateAsync(id, seat0UserId, seat1UserId, record);

    public Task SaveAsync(Guid id, MatchRecord record, MatchStatus status)
    {
        if (FailSaves) throw new IOException("The disk is full.");
        Interlocked.Increment(ref _saves);
        return inner.SaveAsync(id, record, status);
    }

    public Task SetStatusAsync(Guid id, MatchStatus status) => inner.SetStatusAsync(id, status);

    public Task<IReadOnlyList<MatchEntity>> RunningAsync() => inner.RunningAsync();
}
```

Create `tests/CromoBound.Server.Tests/MatchPlayTests.cs`:

```csharp
using CromoBound.Engine;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchPlayTests
{
    private static readonly PlayerId[] Seats = [new(0), new(1)];

    [Fact]
    public async Task Scripted_players_finish_a_bo1_through_the_hub_and_each_only_ever_sees_their_own_view()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        await players.PlayAsync();

        var ended = await players.First.WaitForAsync<MatchEndedNotice>();
        var other = await players.Second.WaitForAsync<MatchEndedNotice>();
        Assert.Equal((players.MatchId, MatchEndReason.Finished), (ended.MatchId, ended.Reason));
        Assert.Contains(ended.Winner, new[] { "alice", "bob" });
        Assert.Equal(1, ended.GameWins.Max());
        Assert.Equal((ended.MatchId, ended.Reason, ended.Winner), (other.MatchId, other.Reason, other.Winner));
        Assert.Equal(ended.GameWins, other.GameWins);
        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
        Assert.Equal(MatchReply.None, await players.First.GetMatchAsync());
        await factory.WithDbAsync(async db => Assert.Equal(MatchStatus.Finished, (await db.Matches.SingleAsync()).Status));
        foreach (var seat in Seats)
        {
            var opponent = 1 - seat.Index;
            var views = players[seat].All<MatchViewNotice>();
            Assert.True(views.Count > 20, $"{seat} received {views.Count} views.");
            Assert.All(views, notice =>
            {
                var view = notice.View;
                Assert.Equal(seat, view.Viewer);
                Assert.Null(view.Players[opponent].Hand);
                Assert.Null(view.Players[opponent].Sideboard);
                Assert.True(view.Decision is null || view.Decision.Players.Contains(seat));
                Assert.All(view.Battlefields ?? [], b => Assert.True(b.Facedown is null || b.Facedown.Controller == seat));
            });
        }
    }

    [Fact]
    public async Task A_rejected_action_returns_the_engines_reason_and_saves_nothing()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var store = factory.Services.GetRequiredService<TestMatchStore>();
        var decider = players.Host.Match.Pending!.Players[0];
        var other = new PlayerId(1 - decider.Index);

        var refused = await players[other].SubmitAsync(players.MatchId, new ChoosePlayOrder(true));

        Assert.False(refused.Accepted);
        Assert.Equal(RejectionCode.NotYourDecision, refused.Rejection!.Code);
        Assert.Equal(0, store.Saves);
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task A_match_the_player_isnt_in_reads_as_not_found_and_an_empty_action_is_refused()
    {
        using var factory = new ServerFactory();
        await using var first = await TwoPlayers.StartAsync(factory);
        await using var second = await TwoPlayers.StartAsync(factory, "carol", "dave");
        var action = new ChoosePlayOrder(true);

        Assert.Equal(MatchRegistry.NotFound, (await first.First.SubmitAsync(second.MatchId, action)).Error);
        Assert.Equal(MatchRegistry.NotFound, (await first.First.SubmitAsync(Guid.NewGuid(), action)).Error);
        Assert.Equal(MatchRegistry.UnreadableAction, (await first.First.SubmitRawAsync(first.MatchId, null)).Error);

        Assert.Equal(MatchStage.PlayOrder, second.Host.Match.Stage);
        Assert.Equal(MatchStage.PlayOrder, first.Host.Match.Stage);
    }

    [Fact]
    public async Task An_action_sent_without_its_type_is_a_plain_error_and_the_connection_stays_open()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players[players.Host.Match.Pending!.Players[0]];

        await Assert.ThrowsAsync<HubException>(() => decider.SubmitRawAsync(players.MatchId, new ChoosePlayOrder(true)));

        Assert.Equal(HubConnectionState.Connected, decider.Connection.State);
        Assert.True((await decider.SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
    }

    [Fact]
    public async Task Actions_sent_by_both_players_at_once_are_both_applied()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players.Host.Match.Pending!.Players[0];
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.IsType<SideboardDecision>(players.Host.Match.Pending);

        var replies = await Task.WhenAll(Seats.Select(seat => players[seat].SubmitAsync(players.MatchId, new SubmitSideboard())));

        Assert.All(replies, reply => Assert.True(reply.Accepted, reply.Rejection?.Message ?? reply.Error));
        Assert.IsType<MulliganDecision>(players.Host.Match.Pending);
        Assert.Equal(3, factory.Services.GetRequiredService<TestMatchStore>().Saves);
    }

    [Fact]
    public async Task Conceding_ends_the_match_for_both_and_frees_the_players()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted);

        foreach (var seat in Seats)
        {
            var ended = await players[seat].WaitForAsync<MatchEndedNotice>();
            Assert.Equal((MatchEndReason.Finished, "bob"), (ended.Reason, ended.Winner));
            Assert.Equal(new[] { 0, 1 }, ended.GameWins);
        }
        Assert.Null((await players.Second.ChallengeAsync("alice", Decks.Second)).Error);
    }

    [Fact]
    public async Task A_failed_save_takes_the_match_back_to_its_last_saved_state()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var store = factory.Services.GetRequiredService<TestMatchStore>();
        var decider = players.Host.Match.Pending!.Players[0];
        var before = TwoPlayers.AsReceived(players.Host.Match.ViewFor(decider));
        var viewsBefore = players[decider].All<MatchViewNotice>().Count;
        store.FailSaves = true;

        var failed = await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true));

        Assert.Equal((false, MatchHost.SaveFailed), (failed.Accepted, failed.Error));
        Assert.Equal(before, TwoPlayers.AsReceived(players.Host.Match.ViewFor(decider)));
        Assert.Equal(viewsBefore, players[decider].All<MatchViewNotice>().Count);
        store.FailSaves = false;
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.IsType<SideboardDecision>(players.Host.Match.Pending);
    }

    [Fact]
    public async Task Every_connection_of_a_player_gets_the_views()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players.Host.Match.Pending!.Players[0];
        await using var secondTab = await GameClient.ConnectAsync(factory, decider.Index == 0 ? "alice" : "bob");

        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);

        var view = await secondTab.WaitForAsync<MatchViewNotice>();
        Assert.Equal((players.MatchId, decider, MatchStage.Sideboarding), (view.MatchId, view.View.Viewer, view.View.Stage));
    }
}
```

Two notes on these tests:
- The second tab connects after the match started, so the first view it receives is the one sent after the play-order choice.
- A view read back by the client has null in place of empty lists (see `TwoPlayers.AsReceived`). That is why the leak test reads `view.Battlefields ?? []`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`SubmitReply`, `MatchHost.SaveFailed`, `MatchRegistry.NotFound` and the rest don't exist).

- [ ] **Step 3: Write the implementation**

Append to `src/CromoBound.Server/Hubs/GameContracts.cs`, and add `using CromoBound.Engine;` at the top:

```csharp

/// <summary>Submit's answer (spec §6.3): whether the engine accepted the action, with the engine's own rejection when it didn't, or the
/// server's reason (no such match, an unreadable action, a save that failed).</summary>
public sealed record SubmitReply(bool Accepted, Rejection? Rejection, string? Error);
```

In `src/CromoBound.Server/Hubs/IGameClient.cs`, add after `View`:

```csharp

    Task MatchEnded(MatchEndedNotice ended);
```

Replace `src/CromoBound.Server/Matches/MatchHost.cs` with:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Hubs;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>One running match (spec §6.6): the engine's match, its two seats and its last saved record. A lock serializes everything
/// that reads or changes the match: an accepted action is saved before anyone sees it, and each player's view is built from a settled
/// state. <paramref name="finished"/> takes the match out of the registry when it ends.</summary>
internal sealed class MatchHost(Guid id, Match match, MatchRecord saved, IReadOnlyList<MatchSeat> seats, IMatchStore store,
    CardDatabase cards, IHubContext<GameHub, IGameClient> hub, ILogger log, Action<MatchHost> finished)
{
    public const string SaveFailed = "The action couldn't be saved, try again.";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private MatchRecord _saved = saved;

    public Guid Id => id;

    /// <summary>The engine's match; replaced by a reload of the last saved record when a save fails.</summary>
    public Match Match { get; private set; } = match;

    public IReadOnlyList<MatchSeat> Seats => seats;

    public PlayerId? SeatOf(int userId)
    {
        for (var i = 0; i < seats.Count; i++)
            if (seats[i].UserId == userId) return new PlayerId(i);
        return null;
    }

    /// <summary>Tells each player the match started, then sends each their first view.</summary>
    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            for (var i = 0; i < seats.Count; i++)
                await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId))
                    .MatchStarted(new MatchStartedNotice(id, seats[1 - i].UserName, new PlayerId(i)));
            await PushViewsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PlayerView> ViewAsync(PlayerId seat)
    {
        await _lock.WaitAsync();
        try
        {
            return Match.ViewFor(seat);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Under the lock, the engine decides; a rejected action changes and saves nothing. An accepted one is saved, then each
    /// player is sent their view. If the save fails, the match goes back to its last saved record, the action is lost and the caller is
    /// asked to try again. A finished match is saved as Finished, leaves the registry, and both players are told.</summary>
    public async Task<SubmitReply> SubmitAsync(PlayerId seat, PlayerAction action)
    {
        await _lock.WaitAsync();
        try
        {
            var result = Match.Submit(seat, action);
            if (!result.Accepted) return new SubmitReply(false, result.Rejection, null);
            var record = Match.ToRecord();
            var over = Match.Stage == MatchStage.Over;
            try
            {
                await store.SaveAsync(id, record, over ? MatchStatus.Finished : MatchStatus.Running);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Saving match {MatchId} failed; it goes back to its last saved state.", id);
                Match = Match.Load(_saved, cards);
                return new SubmitReply(false, null, SaveFailed);
            }
            _saved = record;
            await PushViewsAsync();
            if (over) await EndAsync();
            return new SubmitReply(true, null, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Each player gets their own seat's view, and only that.</summary>
    private async Task PushViewsAsync()
    {
        for (var i = 0; i < seats.Count; i++)
            await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId)).View(new MatchViewNotice(id, Match.ViewFor(new PlayerId(i))));
    }

    /// <summary>Leaves the registry first, so the players are free to play again by the time they hear the match ended.</summary>
    private async Task EndAsync()
    {
        finished(this);
        var result = Match.Result;
        var winner = result.Winner is { } seat ? seats[seat.Index].UserName : null;
        await hub.Clients.Groups([.. seats.Select(s => GameHub.UserGroup(s.UserId))])
            .MatchEnded(new MatchEndedNotice(id, MatchEndReason.Finished, result.GameWins, winner));
    }
}
```

Replace `src/CromoBound.Server/Matches/MatchRegistry.cs` with:

```csharp
using System.Collections.Concurrent;
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>The running matches (spec §6.6), by id and by player. A player is in at most one, and acts only in their own seat.</summary>
internal sealed class MatchRegistry(IMatchStore store, CardDatabase cards, IHubContext<GameHub, IGameClient> hub, ILogger<MatchHost> hostLog)
{
    public const string NotFound = "There is no such match.";
    public const string UnreadableAction = "That action can't be read.";

    private readonly ConcurrentDictionary<Guid, MatchHost> _hosts = new();
    private readonly ConcurrentDictionary<int, MatchHost> _byUser = new();

    public int Count => _hosts.Count;

    public bool IsPlaying(int userId) => _byUser.ContainsKey(userId);

    public MatchHost? Find(Guid matchId) => _hosts.GetValueOrDefault(matchId);

    /// <summary>Holds a match that was just created, or reloaded from <paramref name="saved"/>.</summary>
    public MatchHost Open(Guid id, Match match, MatchRecord saved, IReadOnlyList<MatchSeat> seats)
    {
        var host = new MatchHost(id, match, saved, seats, store, cards, hub, hostLog, Close);
        _hosts[id] = host;
        foreach (var seat in seats) _byUser[seat.UserId] = host;
        return host;
    }

    /// <summary>The user's running match and their view of it, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat), null)
            : MatchReply.None;

    /// <summary>The user acts in their own seat; a match they aren't in reads as missing.</summary>
    public Task<SubmitReply> SubmitAsync(int userId, Guid matchId, PlayerAction? action)
    {
        if (Find(matchId) is not { } host || host.SeatOf(userId) is not { } seat) return Task.FromResult(new SubmitReply(false, null, NotFound));
        if (action is null) return Task.FromResult(new SubmitReply(false, null, UnreadableAction));
        return host.SubmitAsync(seat, action);
    }

    private void Close(MatchHost host)
    {
        _hosts.TryRemove(host.Id, out _);
        foreach (var seat in host.Seats) _byUser.TryRemove(new KeyValuePair<int, MatchHost>(seat.UserId, host));
    }
}
```

In `src/CromoBound.Server/Matches/Lobby.cs`, in `AcceptAsync`, replace the two lines that save and open the match with:

```csharp
            var record = match.ToRecord();
            await store.CreateAsync(id, challenge.From.UserId, challenge.To.UserId, record);
            var host = matches.Open(id, match, record, [challenge.From, challenge.To]);
```

In `src/CromoBound.Server/Hubs/GameHub.cs`:
- Add `using CromoBound.Engine.Actions;`.
- Add after `GetMatch`:

```csharp

    /// <summary>Any action, manual ones, undo and concede included, in the caller's own seat (spec §6.3).</summary>
    public Task<SubmitReply> Submit(Guid matchId, PlayerAction? action) => matches.SubmitAsync(Me.UserId, matchId, action);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): play matches over the hub, saving after every action"
```

---
### Task 5: Restarts

**Files:**
- Modify: `src/CromoBound.Server/Matches/MatchStartup.cs` (replace), `Matches/MatchRegistry.cs`
- Modify: `tests/CromoBound.Server.Tests/TwoPlayers.cs`
- Create: `tests/CromoBound.Server.Tests/RestartTests.cs`

**Interfaces:**
- Consumes:
  - From Task 1: `IMatchStore.RunningAsync`, `IMatchStore.SetStatusAsync`, `MatchStore.Read`, `MatchStore.Json`, `ServerFactory.NewDatabasePath`, `ServerFactory.DeleteDatabase`.
  - From Task 4: `MatchRegistry.Open(Guid, Match, MatchRecord, IReadOnlyList<MatchSeat>)`, `TwoPlayers.PlayAsync`.
  - From the engine: `Match.Load(MatchRecord, CardDatabase)`, which throws `MatchVersionMismatchException`.
- Produces:
  - `MatchRegistry.NoteAbandoned(int userId, MatchEndedNotice ended)`.
  - `MatchRegistry.CurrentAsync` now also returns the abandoned notice, once.
  - In the tests: `TwoPlayers.ReconnectAsync(factory, matchId, first, second)`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Server.Tests/TwoPlayers.cs`, add after `StartAsync`:

```csharp
    /// <summary>Connects the two users of a match that is already running, e.g. after a restart.</summary>
    public static async Task<TwoPlayers> ReconnectAsync(ServerFactory factory, Guid matchId, string first = "alice", string second = "bob") =>
        new(factory, await GameClient.ConnectAsync(factory, first), await GameClient.ConnectAsync(factory, second), matchId);
```

Create `tests/CromoBound.Server.Tests/RestartTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Models.Json;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>A server restart (spec §6.6): running matches come back exactly; one that can't is abandoned, and its players are told
/// once.</summary>
public class RestartTests
{
    /// <summary>Runs a first server over the file, starts a match between alice and bob, lets <paramref name="before"/> act, then stops
    /// the server.</summary>
    private static async Task<Guid> FirstServerAsync(string path, Func<ServerFactory, TwoPlayers, Task> before)
    {
        using var first = new ServerFactory(databasePath: path);
        await using var players = await TwoPlayers.StartAsync(first);
        await before(first, players);
        return players.MatchId;
    }

    private static Task EditRecordAsync(ServerFactory factory, Func<string, string> edit) =>
        factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            row.RecordJson = edit(row.RecordJson);
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task A_restart_in_the_middle_of_a_match_loses_nothing()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            string firstView = "", secondView = "";
            var matchId = await FirstServerAsync(path, async (_, players) =>
            {
                await players.PlayAsync(actions: 12);
                firstView = CromoJson.Serialize((await players.First.GetMatchAsync()).View);
                secondView = CromoJson.Serialize((await players.Second.GetMatchAsync()).View);
            });

            using var second = new ServerFactory(databasePath: path);
            await using var again = await TwoPlayers.ReconnectAsync(second, matchId);
            var current = await again.First.GetMatchAsync();

            Assert.Equal(matchId, current.MatchId);
            Assert.Equal(firstView, CromoJson.Serialize(current.View));
            Assert.Equal(secondView, CromoJson.Serialize((await again.Second.GetMatchAsync()).View));
            await again.PlayAsync();
            Assert.Equal(MatchEndReason.Finished, (await again.First.WaitForAsync<MatchEndedNotice>()).Reason);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_match_saved_by_another_engine_build_is_abandoned_and_its_players_are_told_once()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (first, _) =>
                EditRecordAsync(first, json => MatchStore.Json(MatchStore.Read(json) with { EngineVersion = "0.0.0-old" })));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");
            await using var bob = await GameClient.ConnectAsync(second, "bob");
            var told = await alice.GetMatchAsync();

            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            await second.WithDbAsync(async db => Assert.Equal(MatchStatus.Abandoned, (await db.Matches.SingleAsync()).Status));
            Assert.Null(told.MatchId);
            Assert.Equal((matchId, MatchEndReason.Abandoned, (string?)null), (told.Ended!.MatchId, told.Ended.Reason, told.Ended.Winner));
            Assert.Equal(MatchReply.None, await alice.GetMatchAsync());
            Assert.Equal(MatchEndReason.Abandoned, (await bob.GetMatchAsync()).Ended!.Reason);
            Assert.Null((await alice.ChallengeAsync("bob", Decks.First)).Error);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task An_unreadable_match_record_is_abandoned_and_the_server_still_starts()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (first, _) => EditRecordAsync(first, _ => "{ not a record"));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");

            await second.WithDbAsync(async db => Assert.Equal(MatchStatus.Abandoned, (await db.Matches.SingleAsync()).Status));
            Assert.Equal(matchId, (await alice.GetMatchAsync()).Ended!.MatchId);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_finished_match_stays_finished_and_keeps_its_record()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            await FirstServerAsync(path, async (_, players) =>
                Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");

            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            Assert.Equal(MatchReply.None, await alice.GetMatchAsync());
            await second.WithDbAsync(async db =>
            {
                var row = await db.Matches.SingleAsync();
                Assert.Equal(MatchStatus.Finished, row.Status);
                Assert.Contains(MatchStore.Read(row.RecordJson).Log, logged => logged.Action is Concede);
            });
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }
}
```

Twelve bot actions leave the match running: a Bo1 between the test decks takes far more actions than that to reach 8 points. In `FirstServerAsync` the players' connections close before the first server stops, because `players` is declared after `first` and is disposed first.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`, then `dotnet test CromoBound.slnx --filter "FullyQualifiedName~RestartTests"`.
Expected: FAIL for the first three tests. The second server reloads nothing, so `GetMatch` finds neither the match nor an abandoned notice, and the unreadable row stays Running. `A_finished_match_stays_finished_and_keeps_its_record` already passes; it pins that the reload leaves finished matches alone.

- [ ] **Step 3: Write the implementation**

Replace `src/CromoBound.Server/Matches/MatchStartup.cs` with:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Matches;

/// <summary>Runs at startup, after the database is ready (spec §6.6). Taking the card data loads it, so a data folder that can't be
/// loaded stops the server. Every Running match is replayed from its record. A match that can't be replayed is marked Abandoned, and
/// its players are told the next time they ask for their match. That covers a record from another engine build or other card data,
/// and one that can't be read at all. Nothing in a saved match can stop the server from starting.</summary>
internal sealed class MatchStartup(CardDatabase cards, IMatchStore store, MatchRegistry matches, IServiceScopeFactory scopes,
    ILogger<MatchStartup> log) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Loaded {Count} cards.", cards.Cards.Count);
        var running = await store.RunningAsync();
        if (running.Count == 0) return;
        var names = await UserNamesAsync(cancellationToken);
        foreach (var row in running)
        {
            IReadOnlyList<MatchSeat> seats = [new(row.Seat0UserId, names[row.Seat0UserId]), new(row.Seat1UserId, names[row.Seat1UserId])];
            try
            {
                var record = MatchStore.Read(row.RecordJson);
                matches.Open(row.Id, Match.Load(record, cards), record, seats);
            }
            catch (Exception ex)
            {
                if (ex is MatchVersionMismatchException) log.LogWarning("Match {MatchId} is abandoned: {Reason}", row.Id, ex.Message);
                else log.LogError(ex, "Match {MatchId} can't be reloaded and is abandoned.", row.Id);
                await store.SetStatusAsync(row.Id, MatchStatus.Abandoned);
                var ended = new MatchEndedNotice(row.Id, MatchEndReason.Abandoned, [0, 0], null);
                foreach (var seat in seats) matches.NoteAbandoned(seat.UserId, ended);
            }
        }
        log.LogInformation("Reloaded {Reloaded} of {Running} running matches.", matches.Count, running.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<Dictionary<int, string>> UserNamesAsync(CancellationToken cancel)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.UserName, cancel);
    }
}
```

In `src/CromoBound.Server/Matches/MatchRegistry.cs`:
- Add a field after `_byUser`:

```csharp
    private readonly ConcurrentDictionary<int, MatchEndedNotice> _abandoned = new();
```

- In `Open`, replace `foreach (var seat in seats) _byUser[seat.UserId] = host;` with:

```csharp
        foreach (var seat in seats)
        {
            _byUser[seat.UserId] = host;
            _abandoned.TryRemove(seat.UserId, out _);
        }
```

- Replace `CurrentAsync` with these two methods:

```csharp
    /// <summary>The user's running match and their view of it; otherwise, once, the notice of a match of theirs abandoned at startup;
    /// otherwise nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId)
    {
        if (_byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat)
            return new MatchReply(host.Id, await host.ViewAsync(seat), null);
        return _abandoned.TryRemove(userId, out var ended) ? new MatchReply(null, null, ended) : MatchReply.None;
    }

    /// <summary>Keeps the notice until the user next asks for their match (in memory: a later restart forgets it).</summary>
    public void NoteAbandoned(int userId, MatchEndedNotice ended) => _abandoned[userId] = ended;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): reload running matches on restart and abandon unreadable ones"
```

---

### Task 6: Deployment

**Files:**
- Create: `Dockerfile`, `.dockerignore`, `docs/server-deploy.md`
- Modify: `docs/server.md`

**Interfaces:**
- Consumes:
  - The server's settings (Plan G `ServerOptions`, and `DatabaseSetup.AdminUserKey`/`AdminPasswordKey`).
  - The maintenance endpoints (Task 3).
  - The engine version, `EngineInfo.Version`, which is the engine assembly's informational version: `1.0.0+<SourceRevisionId>`.
- Produces: the image and the deploy guide. No code.

- [ ] **Step 1: Write the Dockerfile and its ignore file**

Create `.dockerignore`:

```
**/bin/
**/obj/
.git/
.vs/
.config/
.superpowers/
docs/
schema/
tests/
tools/
data/raw/
data/import-report.md
*.db
*.db-shm
*.db-wal
```

Create `Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1
# The CromoBound server (docs/server-deploy.md). Build it with the commit it comes from, which becomes part of the engine version
# saved in every match:
#   docker build --build-arg SOURCE_REVISION=$(git rev-parse HEAD) -t cromobound:latest .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SOURCE_REVISION
RUN test -n "$SOURCE_REVISION" || { echo "Build with --build-arg SOURCE_REVISION=\$(git rev-parse HEAD)." >&2; exit 1; }
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/CromoBound.Server/CromoBound.Server.csproj -c Release -o /app \
    -p:UseAppHost=false -p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY data/cards.json data/tokens.json data/printings.json data/sets.json data/
COPY data/effects/ data/effects/
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    CromoBound__DataFolder=/app/data \
    CromoBound__DatabasePath=/var/lib/cromobound/db/cromobound.db \
    CromoBound__KeysFolder=/var/lib/cromobound/keys
RUN mkdir -p /var/lib/cromobound/db /var/lib/cromobound/keys && chown -R $APP_UID /var/lib/cromobound
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CromoBound.Server.dll"]
```

A note on the Dockerfile:
- `$APP_UID` is the non-root user the .NET images define (UID 1654).
- An empty named volume mounted over `/var/lib/cromobound/db` or `/var/lib/cromobound/keys` takes the image folder's owner, so the server can write there.
- The build copies no `.git`, so the commit must come in as `SOURCE_REVISION`. Without it every image would carry the same engine version, and a changed engine would replay old matches as if nothing had changed.

- [ ] **Step 2: Check the publish the Dockerfile runs**

Docker isn't installed on the development machine, so check the publish step directly. Run, from the repository root:

```bash
export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && dotnet publish src/CromoBound.Server/CromoBound.Server.csproj -c Release -o "$TEMP/cromobound-publish" -p:UseAppHost=false -p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false -p:SourceRevisionId=plan-h-check
grep -h AssemblyInformationalVersion src/CromoBound.Engine/obj/Release/net10.0/CromoBound.Engine.AssemblyInfo.cs
ls "$TEMP/cromobound-publish" | grep -i -E "Design|xunit" || echo "no design-time or test assemblies"
rm -rf "$TEMP/cromobound-publish"
```

Expected:
- The publish reports 0 warnings and 0 errors.
- The engine's informational version is `1.0.0+plan-h-check`.
- `no design-time or test assemblies` is printed.

The `obj/Release` folders are ignored by git.

- [ ] **Step 3: Write the deploy guide**

Create `docs/server-deploy.md`:

````markdown
# CromoBound: Deploying the Server

The server runs on one small Linux VPS with Docker Compose: the app in one container and Caddy in front of it for HTTPS. The SQLite
file and the keys that encrypt session cookies live on named volumes, so they survive redeploys. Design: `docs/server.md`.

## 1. What you need
- A Linux VPS (1 vCPU and 1 GB of RAM are plenty) with Docker Engine and the Compose plugin.
- A domain name whose A (and AAAA) record points at the VPS. Pick a name that says nothing about the site: HTTPS certificates are
  listed in public Certificate Transparency logs, so the host name is public even though nothing behind the login is.
- Ports 80 and 443 open, and nothing else.

## 2. Build the image
On the VPS, in a clone of the repository at the commit to deploy:

```bash
git pull
docker build --build-arg SOURCE_REVISION=$(git rev-parse HEAD) -t cromobound:latest .
```

The commit becomes part of the engine version that every saved match records, and the build refuses to run without it. A match saved
by one build can't be replayed by another, so deploy with maintenance on (section 6).

## 3. Compose file and Caddyfile
`/opt/cromobound/compose.yaml`:

```yaml
name: cromobound

services:
  app:
    image: cromobound:latest
    restart: unless-stopped
    environment:
      AllowedHosts: play.example.com
      CromoBound__KnownNetworks__0: 172.30.0.0/24
      # First start only: remove both lines once the admin account exists (section 5).
      CROMOBOUND_ADMIN_USER: owner
      CROMOBOUND_ADMIN_PASSWORD: a-long-first-password
    volumes:
      - db:/var/lib/cromobound/db
      - keys:/var/lib/cromobound/keys
    networks: [web]

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    networks: [web]

networks:
  web:
    ipam:
      config:
        - subnet: 172.30.0.0/24

volumes:
  db:
  keys:
  caddy-data:
  caddy-config:
```

`/opt/cromobound/Caddyfile`:

```
play.example.com {
    encode zstd gzip
    header -Server
    reverse_proxy app:8080
}
```

Replace `play.example.com` in both files with the domain. Caddy gets the certificate, redirects HTTP to HTTPS, passes WebSockets
through, and sends the client's address in `X-Forwarded-For`.

`CromoBound__KnownNetworks__0` must be the `web` network's subnet. The server trusts forwarded headers only from there. Without it,
the server takes Caddy for the client of every request: every login would share one rate limit, and lockouts couldn't tell people
apart. The app service has no `ports:`, so Kestrel's port 8080 is reachable only from Caddy.

## 4. Settings
Environment variables on the `app` service. List settings take `__0`, `__1` and so on.

| Variable | Set by the image | Meaning |
|---|---|---|
| `CromoBound__DatabasePath` | `/var/lib/cromobound/db/cromobound.db` | The SQLite file, on the `db` volume |
| `CromoBound__KeysFolder` | `/var/lib/cromobound/keys` | The keys that encrypt session cookies, on the `keys` volume. Losing them signs everyone out |
| `CromoBound__DataFolder` | `/app/data` | The card data, built into the image |
| `CromoBound__KnownNetworks__0` | (none) | The proxy's network in CIDR form (section 3) |
| `CromoBound__KnownProxies__0` | (none) | Or a proxy's fixed address |
| `CromoBound__LoginRequestsPerMinute` | 10 | Login requests per client address per minute |
| `CromoBound__LockoutFailures`, `CromoBound__LockoutMinutes` | 5, 15 | Failed sign-ins that lock a username, and for how long |
| `CromoBound__CookieHours` | 12 | Sliding session lifetime |
| `CROMOBOUND_ADMIN_USER`, `CROMOBOUND_ADMIN_PASSWORD` | (none) | The first admin, used only while there are no users |
| `AllowedHosts` | `*` | The domain, so requests for other host names are refused |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Never `Development`, which shows error details |

A setting that can't be used (a number below 1, an address or network that can't be read, a card data folder that can't be loaded)
stops the server, and the log names it.

## 5. First start

```bash
cd /opt/cromobound
docker compose up -d
docker compose logs app
```

The log says "Created the first admin account owner." Then remove the two `CROMOBOUND_ADMIN_*` lines from `compose.yaml` and run
`docker compose up -d` again.

Accounts are made with the admin API. The UI comes in Phase 4; until then, use curl with a cookie jar:

```bash
curl -c jar -H 'Content-Type: application/json' -d '{"userName":"owner","password":"a-long-first-password"}' https://play.example.com/login
curl -b jar -H 'Content-Type: application/json' -d '{"userName":"friend1","password":"a-long-password-1","isAdmin":false}' https://play.example.com/api/admin/users
curl -b jar https://play.example.com/api/admin/users
```

Passwords have at least 12 characters, and usernames 3 to 24 letters, digits, `_` or `-`.

## 6. Deploying a new version
1. Turn maintenance on, so no new challenges or matches start:
   `curl -b jar -H 'Content-Type: application/json' -d '{"on":true}' https://play.example.com/api/admin/maintenance`
2. Wait until `runningMatches` is 0: `curl -b jar https://play.example.com/api/admin/maintenance`
3. Build the new image (section 2), then run `docker compose up -d app`.

What a restart does:
- Maintenance is off again, since the switch lives in memory.
- A match still running when a new build starts is abandoned: every new commit changes the engine version. Its players are told the next time they connect.
- Open challenges and login lockouts are cleared, the admin's lockout included.
- Sessions survive, because the keys are on a volume.

## 7. Backups
An online backup of the SQLite file, from a throwaway container:

```bash
mkdir -p /opt/cromobound/backups
docker run --rm -v cromobound_db:/db -v /opt/cromobound/backups:/backup alpine:3.20 \
  sh -c 'apk add --no-cache sqlite >/dev/null && sqlite3 /db/cromobound.db ".backup /backup/cromobound-$(date +%F).db"'
```

Run it daily from cron, and copy `/opt/cromobound/backups` off the VPS. To restore a backup:
1. Stop the app with `docker compose stop app`.
2. Copy the backup over `cromobound.db` in the `cromobound_db` volume, with a throwaway container as above.
3. Start the app again with `docker compose start app`.

The `keys` volume doesn't need a backup: losing it only signs everyone out.

## 8. Checklist
- [ ] `CromoBound__KnownNetworks__0` (or `KnownProxies`) names Caddy's network.
- [ ] The `db` and `keys` volumes are mounted.
- [ ] `CROMOBOUND_ADMIN_PASSWORD` is removed after the first start.
- [ ] `ASPNETCORE_ENVIRONMENT` is `Production`.
- [ ] `Microsoft.AspNetCore.Authorization` logging is never set below `Warning`: lower levels write role identifiers to the log.
- [ ] The app service publishes no ports; only Caddy publishes 80 and 443.
- [ ] `AllowedHosts` is the domain.
- [ ] Backups run, and are copied off the VPS.

The log shows a data protection warning at every start ("No XML encryptor configured"). That is expected: the keys are protected by
the volume's permissions.
````

- [ ] **Step 4: Update the design doc**

In `docs/server.md`, replace section 3's code block and its two bullets with:

````markdown
```
src/CromoBound.Server/
  Program.cs                 composition: storage, accounts, proxies, matches, endpoints, the hub
  ServerJson.cs              the engine's JSON without indentation, for the hub and saved records
  Accounts/                  users, roles, password rules, login and home pages, admin endpoints, sessions, live hub connections
  Matches/                   lobby (challenges), MatchRegistry, MatchHost, match store, startup reload, maintenance switch
  Hubs/                      GameHub, its replies and notices, the client interface, the session filter
  Storage/                   CromoDbContext, entities, migrations
tests/CromoBound.Server.Tests/   WebApplicationFactory + SignalR client tests
Dockerfile, .dockerignore
docs/server-deploy.md
```

- `CromoBound.Server` references `CromoBound.Engine` and `CromoBound.Data`. Card data is loaded once at startup from the configured data folder with `CardRepository` and shared read-only.
- NuGet packages: EF Core SQLite, plus EF Core Design for the `dotnet ef` tool at design time only. The password hasher and SignalR come with ASP.NET Core. The test project adds the ASP.NET Core test host and the SignalR client. The engine and models keep their no-package rule.
````

Replace section 7's paragraph with:

```markdown
`appsettings.json` plus environment variables (`CromoBound__<Setting>`; list items as `__0`, `__1`):

| Setting | Default | Meaning |
|---|---|---|
| `DatabasePath` | `cromobound.db` | The SQLite file |
| `DataFolder` | `data` | The card data folder; one that can't be loaded stops the server |
| `KeysFolder` | the framework's folder | Where the keys that encrypt session cookies are kept |
| `LoginRequestsPerMinute` | 10 | Login requests per client address per minute |
| `LockoutFailures`, `LockoutMinutes` | 5, 15 | Failed sign-ins that lock a username, and for how long |
| `CookieHours` | 12 | Sliding session lifetime |
| `KnownProxies` | none | Proxy addresses whose forwarded headers are trusted |
| `KnownNetworks` | none | Proxy networks (CIDR) whose forwarded headers are trusted, for a proxy without a fixed address |

The first admin comes from `CROMOBOUND_ADMIN_USER` and `CROMOBOUND_ADMIN_PASSWORD` (§5.3). A number below 1, or a proxy entry that can't be read, stops the server, and the log names the setting.
```

Add these two bullets at the end of section 8:

```markdown
- The image is built with the commit it comes from (`--build-arg SOURCE_REVISION`), which becomes part of the engine version saved in every match. Without it every image would carry the same version, and a changed engine would replay old matches wrongly.
- Before a deploy, maintenance goes on and running matches are left to finish. A match still running when a new build starts is abandoned (§6.6).
```

- [ ] **Step 5: Check and commit**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (nothing in code changed).

Check the new files for em and en dashes (expect no output): `LC_ALL=C.UTF-8 grep -nP '[\x{2013}\x{2014}]' Dockerfile .dockerignore docs/server-deploy.md`.

```bash
git add Dockerfile .dockerignore docs/server-deploy.md docs/server.md
git commit -m "feat(server): add the docker image and deploy guide"
```

---

## Done criteria

- [ ] The database is built by EF Core migrations, and the Matches table keeps every match's record (Task 1).
- [ ] The card data loads at startup, and a folder that can't be loaded stops the server (Task 1).
- [ ] The hub needs a signed-in player, and a changed account closes its live connections at once (Task 2).
- [ ] Players challenge, decline and cancel, and a challenge needs another enabled player and a legal deck (Task 2).
- [ ] Accepting starts a match with the challenger in seat 0, and each player is sent only their own view (Task 3).
- [ ] Maintenance blocks new challenges and matches but not running ones, and only admins can see or switch it (Task 3).
- [ ] Scripted players finish a Bo1 through the hub, and every view a player receives is their own (Task 4).
- [ ] Rejected actions save nothing, a failed save goes back to the last saved state, and a finished match frees its players (Task 4).
- [ ] A restart resumes running matches exactly. Records that can't be replayed are abandoned, their players are told once, and the server still starts (Task 5).
- [ ] The Dockerfile builds a non-root image stamped with its commit, and `docs/server-deploy.md` covers the VPS, Caddy, settings, deploys and backups (Task 6).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
