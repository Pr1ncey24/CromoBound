using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TurnPointTests
{
    [Fact]
    public void No_pause_without_cards_for_that_moment()
    {
        var game = new TestGame();
        game.Put("unit-2", Place.Base(P1));
        game.Put("temp-1", Place.Base(P1));

        var engine = game.Start();
        engine.Accept(P1, new EndTurn());

        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Pauses_at_the_start_of_your_beginning_phase_before_scoring()
    {
        var game = new TestGame();
        var relic = game.Put("dawn-relic", Place.Base(P1));
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));

        var engine = game.Start();

        var pause = engine.Decision<TurnPointDecision>();
        Assert.Equal((P1, TurnPoint.StartOfBeginning), (pause.Player, pause.Point));
        Assert.Equal(new[] { relic }, pause.Cards);
        Assert.Equal(0, game.State.Player(P1).Points);

        engine.Accept(P1, new ContinueTurn());

        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Your_cards_dont_pause_on_the_opponents_turn_but_each_player_cards_do()
    {
        var mine = new TestGame();
        mine.Put("dawn-relic", Place.Base(P2));
        Assert.IsType<PriorityDecision>(mine.Start().Pending);

        var each = new TestGame();
        each.Put("each-relic", Place.Base(P2));
        Assert.Equal(P2, each.Start().Decision<TurnPointDecision>().Player);
    }

    [Fact]
    public void The_turn_player_continues_first_then_the_opponent()
    {
        var game = new TestGame();
        game.Put("dawn-relic", Place.Base(P1));
        game.Put("each-relic", Place.Base(P2));
        var engine = game.Start();

        Assert.Equal(P1, engine.Decision<TurnPointDecision>().Player);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new EndTurn()).Rejection!.Code);
        engine.Accept(P1, new ContinueTurn());
        Assert.Equal(P2, engine.Decision<TurnPointDecision>().Player);
        engine.Accept(P2, new ContinueTurn());

        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Pauses_at_the_start_of_main_and_at_end_of_turn_before_pools_empty()
    {
        var game = new TestGame();
        game.Put("noon-relic", Place.Base(P1));
        game.Put("dusk-relic", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        Assert.Equal(TurnPoint.StartOfMain, engine.Decision<TurnPointDecision>().Point);
        engine.Accept(P1, new ContinueTurn());
        var rune = game.First(Place.Base(P1), "fury-rune");
        engine.Accept(P1, new UseRune(rune, RuneUse.Exhaust));

        engine.Accept(P1, new EndTurn());

        Assert.Equal(TurnPoint.EndOfTurn, engine.Decision<TurnPointDecision>().Point);
        Assert.Equal(1, game.State.Player(P1).Pool.Energy);
        engine.Accept(P1, new ContinueTurn());
        Assert.True(game.State.Player(P1).Pool.IsEmpty);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }
}
