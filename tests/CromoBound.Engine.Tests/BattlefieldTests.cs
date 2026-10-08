using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class BattlefieldTests
{
    private static StandardMove Move(ObjectId unit, Place to) => new() { Units = [unit], Destination = to };

    [Fact]
    public void Moving_to_an_empty_battlefield_starts_a_showdown_and_conquers_after_passes()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));

        Assert.True(game.State[unit].Exhausted);
        Assert.Equal(1, game.State.Showdown!.Battlefield);
        Assert.Equal(P1, game.State.Turn.Focus);
        var focus = engine.Decision<PriorityDecision>();
        Assert.True(focus.CanPass);
        Assert.False(focus.CanEndTurn);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Null(game.State.Showdown);
        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.True(engine.Decision<PriorityDecision>().CanEndTurn);
    }

    [Fact]
    public void Units_can_only_move_where_the_rules_allow()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var ganker = game.Put("ganker-2", Place.Battlefield(0));
        var engine = game.Start();

        var moves = engine.Decision<PriorityDecision>().Moves;

        Assert.Equal(new[] { Place.Base(P1) }, moves.Single(m => m.Unit == unit).Destinations);
        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(1) }, moves.Single(m => m.Unit == ganker).Destinations);
        Assert.Equal(RejectionCode.IllegalLocation, engine.Submit(P1, Move(unit, Place.Battlefield(1))).Rejection!.Code);
    }

    [Fact]
    public void Leaving_a_battlefield_empty_loses_control()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var engine = game.Start();

        engine.Accept(P1, Move(unit, Place.Base(P1)));

        Assert.Null(game.State.Battlefields[0].Controller);
    }

    [Fact]
    public void Conquering_for_the_final_point_needs_every_battlefield_scored_this_turn()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 7;
        game.State.Battlefields[0].Controller = P2;
        game.Put("unit-2", Place.Battlefield(0), owner: P2);
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var handSize = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(7, game.State.Player(P1).Points);
        Assert.Equal(handSize + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Null(engine.Outcome);
    }

    [Fact]
    public void Holding_one_battlefield_and_conquering_the_other_wins()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 6;
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        Assert.Equal(7, game.State.Player(P1).Points);

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(new GameOutcome(P1, GameEndReason.Points), engine.Outcome);
    }

    [Fact]
    public void Focus_passes_after_the_focus_holders_chain_closes()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Put("action-spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, Move(unit, Place.Battlefield(1)));

        var spell = game.First(Place.Hand(P1), "action-spell");
        Assert.Contains(spell, engine.Decision<PriorityDecision>().Playable);
        engine.Accept(P1, new PlayCard(spell));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new ResolveDone());

        Assert.Equal(P2, game.State.Turn.Focus);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Combat_attacker_assigns_first_damage_is_simultaneous_and_the_winner_conquers()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var attacker = game.Put("unit-3", Place.Base(P1));
        game.State[attacker].Modifiers.Add(new MightModifier(1, Duration.ThisCombat));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        var combat = game.State.Showdown!;
        Assert.True(combat.IsCombat);
        Assert.Equal(P1, combat.Attacker);
        Assert.Equal(CombatRole.Attacker, game.State[attacker].Role);
        Assert.Equal(CombatRole.Defender, game.State[defender].Role);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var assign = engine.Decision<AssignDamageDecision>();
        Assert.Equal((P1, 4), (assign.Player, assign.Total));
        Assert.Equal(2, Assert.Single(assign.Targets).Lethal);
        engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 4)] });
        Assert.Equal(P2, engine.Decision<AssignDamageDecision>().Player);
        engine.Accept(P2, new AssignDamage { Assignments = [new(attacker, 2)] });

        Assert.False(game.State.Exists(defender));
        Assert.Equal(0, game.State[attacker].Damage);
        Assert.Null(game.State[attacker].Role);
        Assert.Empty(game.State[attacker].Modifiers);
        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.Null(game.State.Showdown);
    }

    [Fact]
    public void Surviving_attackers_are_recalled_and_a_side_with_no_damage_is_not_asked()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-3", Place.Battlefield(1), owner: P2);
        game.State[defender].Stunned = true;
        var attacker = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var result = engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 2)] });

        Assert.Equal(Place.Base(P1), game.State[attacker].Place);
        Assert.True(game.State[attacker].Exhausted);
        Assert.Equal(0, game.State[defender].Damage);
        Assert.Equal(P2, game.State.Battlefields[1].Controller);
        Assert.Contains(result.Events, e => e is CombatEnded { Result: CombatResult.DefenderWon });
    }

    [Fact]
    public void Tank_units_must_take_lethal_damage_first()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var tank = game.Put("tank-2", Place.Battlefield(1), owner: P2);
        var plain = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var a = game.Put("unit-3", Place.Base(P1));
        var b = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();
        engine.Accept(P1, new StandardMove { Units = [a, b], Destination = Place.Battlefield(1) });
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var wrong = engine.Submit(P1, new AssignDamage { Assignments = [new(plain, 6)] });
        var suggested = engine.Decision<AssignDamageDecision>().Suggested;

        Assert.Equal(RejectionCode.InvalidAssignment, wrong.Rejection!.Code);
        Assert.Equal(tank, suggested[0].Unit);
        engine.Accept(P1, new AssignDamage { Assignments = suggested });
        Assert.Equal(P2, engine.Decision<AssignDamageDecision>().Player);
    }

    [Fact]
    public void When_both_sides_die_the_battlefield_becomes_uncontrolled()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var attacker = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 2)] });
        engine.Accept(P2, new AssignDamage { Assignments = [new(attacker, 2)] });

        Assert.Null(game.State.Battlefields[1].Controller);
        Assert.Null(game.State.Battlefields[1].ContestedBy);
        Assert.Equal(0, game.State.Player(P1).Points);
    }
}
