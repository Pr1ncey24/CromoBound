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
