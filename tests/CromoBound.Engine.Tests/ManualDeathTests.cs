using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

/// <summary>A unit with a death ability the engine runs, moved by hand from the board to a trash: its controller is asked whether
/// it dies. Automatic deaths trigger without asking (TriggerTests).</summary>
public class ManualDeathTests
{
    /// <summary>P1 has Soaring Scout (Deathknell: channel 1 rune exhausted) in their Base and three runes in their Rune Deck.</summary>
    private static (TestGame Test, Game Engine, ObjectId Scout) ScoutGame()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soaring-scout"));
        for (var i = 0; i < 3; i++) game.Put("fury-rune", Place.RuneDeck(P1));
        var scout = game.Put("soaring-scout", Place.Base(P1));
        game.Put("spell", Place.Hand(P1));
        return (game, game.Start(), scout);
    }

    [Fact]
    public void A_unit_moved_by_hand_to_the_trash_dies_when_its_controller_says_so()
    {
        var (game, engine, scout) = ScoutGame();

        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(scout, Place.Trash(P1))).Accepted);
        var ask = engine.Decision<OptionalDecision>();
        Assert.Equal(P1, ask.Player);
        Assert.Equal("soaring-scout", ask.CardId);
        Assert.Equal(Place.Base(P1), game.State[scout].Place);
        var answered = engine.Accept(P1, new ChooseOptional(true));

        Assert.Contains(answered.Events, e => e is UnitDied { CardId: "soaring-scout" });
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "soaring-scout");
        Assert.Equal("soaring-scout", Assert.Single(game.State.Chain).SourceCardId);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        Assert.Equal(1, engine.RunesOf(P1).Count(r => r.Exhausted));
    }

    [Fact]
    public void A_unit_that_doesnt_die_just_moves()
    {
        var (game, engine, scout) = ScoutGame();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(scout, Place.Trash(P1))).Accepted);

        var answered = engine.Accept(P1, new ChooseOptional(false));

        Assert.DoesNotContain(answered.Events, e => e is UnitDied);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "soaring-scout");
        Assert.Empty(game.State.Chain);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void The_units_controller_answers_whoever_moved_it()
    {
        var (_, engine, scout) = ScoutGame();

        Assert.True(engine.SubmitManual(P2, new ManualMoveCard(scout, Place.Trash(P1))).Accepted);

        Assert.Equal(P1, engine.Decision<OptionalDecision>().Player);
        Assert.Equal(RejectionCode.NotYourDecision, engine.Submit(P2, new ChooseOptional(true)).Rejection!.Code);
    }

    [Fact]
    public void Nothing_else_is_accepted_until_the_question_is_answered()
    {
        var (_, engine, scout) = ScoutGame();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(scout, Place.Trash(P1))).Accepted);

        Assert.Equal(RejectionCode.UnexpectedAction, engine.SubmitManual(P2, new ManualAdjustXp(P2, 1)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new Pass()).Rejection!.Code);
        Assert.IsType<OptionalDecision>(engine.Pending);
    }

    [Fact]
    public void Units_without_a_death_ability_and_moves_elsewhere_ask_nothing()
    {
        var (game, engine, scout) = ScoutGame();
        var plain = game.Put("unit-2", Place.Base(P1));

        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(plain, Place.Trash(P1))).Accepted);
        Assert.IsType<PriorityDecision>(engine.Pending);
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(scout, Place.Hand(P1))).Accepted);
        Assert.IsType<PriorityDecision>(engine.Pending);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void During_a_hand_resolution_the_resolution_comes_back_and_the_trigger_follows_it()
    {
        var (game, engine, scout) = ScoutGame();
        game.Runes(P1, "fury-rune", 1);
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Decision<ResolveManuallyDecision>();

        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(scout, Place.Trash(P1))).Accepted);
        engine.Accept(P1, new ChooseOptional(true));

        engine.Decision<ResolveManuallyDecision>();
        engine.Accept(P1, new ResolveDone());
        Assert.Equal("soaring-scout", Assert.Single(game.State.Chain).SourceCardId);
    }
}
