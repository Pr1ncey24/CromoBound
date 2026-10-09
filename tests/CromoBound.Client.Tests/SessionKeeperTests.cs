using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;

namespace CromoBound.Client.Tests;

public class SessionKeeperTests
{
    [Fact]
    public async Task The_keeper_pings_the_server_every_thirty_minutes()
    {
        var session = new SessionState();
        var api = new FakeServerApi(session);
        var time = new ManualTimeProvider();
        await using var keeper = new SessionKeeper(api, time);

        keeper.Start();
        keeper.Start();
        await WaitUntilAsync(() => time.Periods.Count == 1);
        time.Tick();
        await WaitUntilAsync(() => api.MeCalls == 1);
        time.Tick();
        await WaitUntilAsync(() => api.MeCalls == 2);

        Assert.Equal(TimeSpan.FromMinutes(30), SessionKeeper.Interval);
        Assert.Equal(new[] { SessionKeeper.Interval }, time.Periods);
    }

    [Fact]
    public async Task A_ping_that_finds_the_session_over_ends_it()
    {
        var session = new SessionState();
        var api = new FakeServerApi(session) { SessionOver = true };
        await using var keeper = new SessionKeeper(api, TimeProvider.System);

        await keeper.PingAsync();

        Assert.True(session.IsEnded);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }
}
