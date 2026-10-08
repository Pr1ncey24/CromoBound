using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TurnTests
{
    [Fact]
    public void First_turn_channels_two_runes_draws_one_and_gives_priority()
    {
        var game = new TestGame();
        for (var i = 0; i < 5; i++) game.Put("fury-rune", Place.RuneDeck(P1));

        var engine = game.Start();

        Assert.Equal(2, game.State.At(Place.Base(P1)).Count);
        Assert.Single(game.State.At(Place.Hand(P1)));
        Assert.Equal(9, game.State.At(Place.MainDeck(P1)).Count);
        Assert.Equal(Phase.Main, game.State.Turn.Phase);
        var priority = engine.Decision<PriorityDecision>();
        Assert.Equal(P1, priority.Player);
        Assert.True(priority.CanEndTurn);
        Assert.False(priority.CanPass);
    }

    [Fact]
    public void Second_player_channels_an_extra_rune_on_their_first_turn()
    {
        var game = new TestGame();
        for (var i = 0; i < 5; i++) game.Put("chaos-rune", Place.RuneDeck(P2));
        var engine = game.Start();

        engine.Accept(P1, new EndTurn());

        Assert.Equal(2, game.State.Turn.Number);
        Assert.Equal(P2, game.State.Turn.TurnPlayer);
        Assert.Equal(3, game.State.At(Place.Base(P2)).Count);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Runes_float_energy_pools_empty_at_end_of_turn_and_awaken_readies()
    {
        var game = new TestGame();
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        var rune = game.State.At(Place.Base(P1))[0];

        engine.Accept(P1, new UseRune(rune, RuneUse.Exhaust));
        Assert.Equal(1, game.State.Player(P1).Pool.Energy);
        Assert.True(game.State[rune].Exhausted);

        engine.Accept(P1, new EndTurn());
        Assert.True(game.State.Player(P1).Pool.IsEmpty);

        engine.Accept(P2, new EndTurn());
        Assert.False(game.State[rune].Exhausted);
    }

    [Fact]
    public void Wrong_player_or_wrong_action_is_rejected_without_changes()
    {
        var game = new TestGame();
        var engine = game.Start();

        var wrongPlayer = engine.Submit(P2, new EndTurn());
        var wrongAction = engine.Submit(P1, new Pass());

        Assert.Equal(RejectionCode.NotYourDecision, wrongPlayer.Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, wrongAction.Rejection!.Code);
        Assert.Equal(1, game.State.Turn.Number);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Empty_deck_burns_out_giving_the_opponent_a_point()
    {
        var game = new TestGame();
        game.Put("unit-3", Place.Trash(P1));
        game.Put("unit-3", Place.Trash(P1));

        game.Start(filler: 0);

        Assert.Equal(1, game.State.Player(P2).Points);
        Assert.Single(game.State.At(Place.Hand(P1)));
        Assert.Single(game.State.At(Place.MainDeck(P1)));
        Assert.Empty(game.State.At(Place.Trash(P1)));
        Assert.Contains(game.StartEvents, e => e is BurnedOut b && b.Player == P1);
    }

    [Fact]
    public void Reaching_eight_points_with_the_lead_wins_in_cleanup()
    {
        var game = new TestGame();
        game.State.Player(P2).Points = 7;

        var engine = game.Start(filler: 0);

        Assert.Equal(new GameOutcome(P2, GameEndReason.Points), engine.Outcome);
        Assert.Null(engine.Pending);
        Assert.Equal(RejectionCode.MatchOver, engine.Submit(P1, new EndTurn()).Rejection!.Code);
    }

    [Fact]
    public void A_tie_at_eight_continues()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 8;
        game.State.Player(P2).Points = 8;

        var engine = game.Start();

        Assert.Null(engine.Outcome);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Hold_scores_controlled_battlefields_at_the_start_of_turn()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));

        game.Start();

        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.Contains(game.StartEvents, e => e is BattlefieldScored { Kind: ScoreKind.Hold, Battlefield: 0, GainedPoint: true });
    }

    [Fact]
    public void Temporary_permanents_die_at_the_start_of_their_controllers_turn()
    {
        var game = new TestGame();
        game.Put("temp-1", Place.Base(P1));

        game.Start();

        Assert.Empty(game.State.At(Place.Base(P1)));
        Assert.Single(game.State.At(Place.Trash(P1)));
    }

    [Fact]
    public void Ending_kills_lethal_damage_then_heals_and_expires_this_turn_effects()
    {
        var game = new TestGame();
        var survivor = game.Put("unit-3", Place.Base(P1));
        var doomed = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var unit = game.State[survivor];
        unit.Damage = 1;
        unit.Stunned = true;
        unit.Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        unit.Modifiers.Add(new MightModifier(1, Duration.Permanent));
        game.State[doomed].Damage = 2;

        engine.Accept(P1, new EndTurn());

        Assert.Equal(0, unit.Damage);
        Assert.False(unit.Stunned);
        Assert.Equal(4, engine.MightOf(survivor));
        Assert.False(game.State.Exists(doomed));
        Assert.Single(game.State.At(Place.Trash(P1)));
    }
}
