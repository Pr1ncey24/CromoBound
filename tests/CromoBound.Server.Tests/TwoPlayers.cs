using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.State;
using CromoBound.Engine.Tests;
using CromoBound.Engine.Views;
using CromoBound.Models.Json;
using CromoBound.Server.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>Two players connected to the hub with a running Bo1 between them: the challenger in seat 0, the other in seat 1.</summary>
internal sealed class TwoPlayers : IAsyncDisposable
{
    private readonly ServerFactory _factory;
    private readonly Bot[] _bots = [new(), new()];

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

    /// <summary>Connects the two users of a match that is already running, e.g. after a restart.</summary>
    public static async Task<TwoPlayers> ReconnectAsync(ServerFactory factory, Guid matchId, string first = "alice", string second = "bob") =>
        new(factory, await GameClient.ConnectAsync(factory, first), await GameClient.ConnectAsync(factory, second), matchId);

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

    /// <summary>A view as a player receives it: written to the wire and read back. Compare views a client received with this, not with
    /// the engine's view directly.</summary>
    public static string AsReceived(PlayerView view) =>
        CromoJson.Serialize(JsonSerializer.Deserialize<PlayerView>(JsonSerializer.Serialize(view, WireJson.Options), WireJson.Options));

    public async ValueTask DisposeAsync()
    {
        await First.DisposeAsync();
        await Second.DisposeAsync();
    }
}
