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

    [Fact]
    public void A_showdown_started_during_an_end_of_turn_pause_finishes_before_the_next_turn()
    {
        var game = new TestGame();
        game.Put("dusk-relic", Place.Base(P1));
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        engine.Accept(P1, new EndTurn());
        Assert.Equal(TurnPoint.EndOfTurn, engine.Decision<TurnPointDecision>().Point);

        game.State.Move(unit, Place.Battlefield(1));
        game.State.Battlefields[1].ContestedBy = P1;
        engine.Accept(P1, new ContinueTurn());

        Assert.NotNull(game.State.Showdown);
        Assert.Equal(1, game.State.Turn.Number);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
        Assert.Equal(P1, game.State.Turn.Focus);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Null(game.State.Showdown);
        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(2, game.State.Turn.Number);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void A_combat_started_during_an_end_of_turn_pause_resolves_before_the_next_turn()
    {
        var game = new TestGame();
        game.Put("dusk-relic", Place.Base(P1));
        var unit = game.Put("unit-2", Place.Base(P1));
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var engine = game.Start();
        engine.Accept(P1, new EndTurn());
        Assert.Equal(TurnPoint.EndOfTurn, engine.Decision<TurnPointDecision>().Point);

        game.State.Move(unit, Place.Battlefield(1));
        game.State.Battlefields[1].ContestedBy = P1;
        engine.Accept(P1, new ContinueTurn());

        Assert.True(game.State.Showdown!.IsCombat);
        Assert.Equal(1, game.State.Turn.Number);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(1, game.State.Turn.Number);
        var attackerAssign = engine.Decision<AssignDamageDecision>();
        Assert.Equal(P1, attackerAssign.Player);
        engine.Accept(P1, new AssignDamage { Assignments = attackerAssign.Suggested });
        var defenderAssign = engine.Decision<AssignDamageDecision>();
        Assert.Equal(P2, defenderAssign.Player);
        engine.Accept(P2, new AssignDamage { Assignments = defenderAssign.Suggested });

        Assert.Null(game.State.Showdown);
        Assert.Equal(2, game.State.Turn.Number);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void A_full_card_never_pauses()
    {
        var game = new TestGame(db: EngineTestDb.Create(("dawn-relic", """{ "cardId": "dawn-relic", "status": "Full" }""")));
        game.Put("dawn-relic", Place.Base(P1));

        var engine = game.Start();

        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_partial_card_pauses_only_for_its_manual_lines()
    {
        var manual = new TestGame(db: EngineTestDb.Create(("dawn-relic", """{ "cardId": "dawn-relic", "status": "Partial" }""")));
        manual.Put("dawn-relic", Place.Base(P1));
        var covered = new TestGame(db: EngineTestDb.Create(("dawn-relic", """
            { "cardId": "dawn-relic", "status": "Partial",
              "abilities": [ { "kind": "Activated", "line": 1, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
            """)));
        covered.Put("dawn-relic", Place.Base(P1));

        Assert.IsType<TurnPointDecision>(manual.Start().Pending);
        Assert.IsType<PriorityDecision>(covered.Start().Pending);
    }
}
