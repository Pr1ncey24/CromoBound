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
