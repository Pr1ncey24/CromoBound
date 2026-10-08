using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TestGameTests
{
    [Fact]
    public void Starts_with_two_battlefields()
    {
        var game = new TestGame();

        Assert.Equal(2, game.State.Battlefields.Count);
        Assert.Equal("bf-a", game.State[game.State.Battlefields[0].Card].CardId);
        Assert.Equal(P2, game.State[game.State.Battlefields[1].Card].Owner);
    }

    [Fact]
    public void Put_creates_cards_and_tokens_where_asked()
    {
        var game = new TestGame();

        var unit = game.Put("unit-3", Place.Battlefield(1), owner: P2);
        var token = game.Put("token-recruit", Place.Base(P1));

        Assert.Equal(P2, game.State[unit].Controller);
        Assert.Equal("p-unit-3", game.State[unit].PrintingId);
        Assert.True(game.State[token].IsToken);
        Assert.Equal(new[] { unit }, game.State.At(Place.Battlefield(1)));
    }

    [Fact]
    public void Test_cards_carry_their_keywords_and_stats()
    {
        var db = EngineTestDb.Create();

        Assert.Equal(3, db.Cards["unit-3"].Might);
        Assert.Contains(Models.Cards.DisplayKeyword.Reaction, db.Cards["reaction-spell"].Keywords);
        Assert.Equal("unit-3", db.Printings["p-unit-3"].CardId);
    }
}
