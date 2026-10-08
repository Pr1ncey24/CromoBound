using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ScriptedGameTests
{
    [Fact]
    public void A_scripted_game_reaches_a_winner_and_replays_identically()
    {
        var first = Play(seed: 3);
        var second = Play(seed: 3);

        Assert.NotNull(first.Outcome);
        Assert.NotNull(first.Outcome!.Winner);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.State.Turn.Number, second.State.Turn.Number);
        Assert.Equal(first.State.Players.Select(p => p.Points), second.State.Players.Select(p => p.Points));
    }

    /// <summary>Two scripted players with 20 unit-2 and 12 fury runes each, until the game ends (or a safety cap).</summary>
    private static Game Play(ulong seed)
    {
        var game = new TestGame(seed);
        foreach (var player in new[] { P1, P2 })
        {
            for (var i = 0; i < 20; i++) game.Put("unit-2", Place.MainDeck(player));
            for (var i = 0; i < 12; i++) game.Put("fury-rune", Place.RuneDeck(player));
        }
        var engine = game.Start(filler: 0);
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 5000 && engine.Outcome is null; i++)
        {
            var player = engine.Pending!.Players[0];
            engine.Accept(player, bots[player.Index].Choose(engine));
        }
        return engine;
    }
}
