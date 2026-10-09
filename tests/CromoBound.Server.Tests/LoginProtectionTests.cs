using System.Net;
using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class LoginProtectionTests
{
    private const string Wrong = "wrong-password-1";

    /// <summary>A real hasher that counts its password checks: all of them, and those against the account named player1.</summary>
    private sealed class CountingHasher : PasswordHasher<UserEntity>
    {
        private int _checks;
        private int _player1Checks;

        public int Checks => Volatile.Read(ref _checks);
        public int Player1Checks => Volatile.Read(ref _player1Checks);

        public override PasswordVerificationResult VerifyHashedPassword(UserEntity user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref _checks);
            if (user.UserName == "player1") Interlocked.Increment(ref _player1Checks);
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private static ServerFactory CountingFactory(CountingHasher hasher) =>
        new(services: services => services.AddSingleton<IPasswordHasher<UserEntity>>(hasher));

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

    [Fact]
    public async Task Parallel_guesses_at_one_name_get_no_more_password_checks_than_the_lockout_allows()
    {
        var hasher = new CountingHasher();
        using var factory = CountingFactory(hasher);
        await factory.AddUserAsync("player1");

        var answers = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => TryAsync(factory, "player1", Wrong)));

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode));
        Assert.InRange(hasher.Player1Checks, 1, 5);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TryAsync(factory, "player1", ServerFactory.PlayerPassword)).StatusCode);
    }

    [Fact]
    public async Task Every_kind_of_failed_sign_in_spends_exactly_one_password_check()
    {
        var hasher = new CountingHasher();
        using var factory = CountingFactory(hasher);
        await factory.AddUserAsync("player1");
        await factory.AddUserAsync("gone");
        await factory.WithStoreAsync(async store => await store.SetDisabledAsync((await store.FindAsync("gone"))!, true));
        for (var i = 0; i < 5; i++) await TryAsync(factory, "locked1", Wrong);

        foreach (var (userName, password) in new[]
        {
            ("player1", Wrong),
            ("nobody-here", Wrong),
            ("not a name!", Wrong),
            ("gone", ServerFactory.PlayerPassword),
            ("locked1", Wrong),
        })
        {
            var before = hasher.Checks;
            Assert.Equal(HttpStatusCode.Unauthorized, (await TryAsync(factory, userName, password)).StatusCode);
            Assert.True(hasher.Checks - before == 1, $"{userName}: {hasher.Checks - before} password checks.");
        }
    }
}
