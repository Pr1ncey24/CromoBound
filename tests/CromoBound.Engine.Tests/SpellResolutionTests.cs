using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class SpellResolutionTests
{
    private const string DealOnLineTwo = """
        { "cardId": "multi-spell", "status": "Partial", "keywords": [ { "keyword": "Reaction" } ],
          "abilities": [ { "kind": "Spell", "line": 2,
            "steps": [ { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>Plays the card from P1's hand with the given targets (one list per slot), pays with the suggestion, and both players pass.</summary>
    private static SubmitResult PlayAndResolve(TestGame game, Game engine, string cardId, params ObjectId[][] targets)
    {
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), cardId)));
        foreach (var slot in targets) engine.Accept(P1, new ChooseTargets { Targets = slot });
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        return engine.Accept(P2, new Pass());
    }

    [Fact]
    public void A_full_spell_resolves_automatically()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        var result = PlayAndResolve(game, engine, "spell", [enemy]);

        Assert.False(game.State.Exists(enemy));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.Contains(result.Events, e => e is ChainItemResolved);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void A_mapped_spell_without_chosen_targets_kills_nothing()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var mine = game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        engine.PayWithSuggestion(P1);
        Assert.Single(game.State.Chain).Effect = null;

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.True(game.State.Exists(mine));
        Assert.True(game.State.Exists(enemy));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void A_partial_spell_runs_its_mapped_lines_then_asks_for_the_rest()
    {
        var game = new TestGame(db: EngineTestDb.Create(("multi-spell", DealOnLineTwo)));
        game.Put("multi-spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        PlayAndResolve(game, engine, "multi-spell", [enemy]);

        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal("<p>Draw 1.</p>", resolve.Text);
        Assert.Equal(3, game.State[enemy].Damage);
        engine.Accept(P1, new ResolveDone());
        Assert.False(game.State.Exists(enemy));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "multi-spell");
    }

    [Fact]
    public void A_target_that_left_before_resolution_is_skipped()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var mine = game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());

        Assert.True(engine.SubmitManual(P2, new ManualMoveCard(enemy, Place.Hand(P2))).Accepted);
        engine.Accept(P2, new Pass());

        Assert.True(game.State.Exists(mine));
        Assert.Contains(game.State.At(Place.Hand(P2)), id => game.State[id].CardId == "unit-3");
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Progress_day_draws_four()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("progress-day", "mind-rune"));
        game.Put("progress-day", Place.Hand(P1));
        game.Runes(P1, "mind-rune", 7);
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;

        PlayAndResolve(game, engine, "progress-day");

        Assert.Equal(hand - 1 + 4, game.State.At(Place.Hand(P1)).Count);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Vengeance_kills_the_chosen_unit()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("vengeance", "order-rune"));
        game.Put("vengeance", Place.Hand(P1));
        game.Runes(P1, "order-rune", 6);
        var mine = game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        PlayAndResolve(game, engine, "vengeance", [enemy]);

        Assert.False(game.State.Exists(enemy));
        Assert.True(game.State.Exists(mine));
    }

    [Fact]
    public void Falling_star_can_hit_the_same_unit_twice()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("falling-star"));
        game.Put("falling-star", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 4);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        game.State[enemy].Modifiers.Add(new MightModifier(3, Duration.Permanent));
        var engine = game.Start();

        PlayAndResolve(game, engine, "falling-star", [enemy], [enemy]);

        Assert.False(game.State.Exists(enemy));
    }

    [Fact]
    public void Vanguard_sergeant_plays_as_a_vanilla_unit()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("vanguard-sergeant", "order-rune"));
        game.Put("vanguard-sergeant", Place.Hand(P1));
        game.Runes(P1, "order-rune", 4);
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "vanguard-sergeant")));
        engine.PayWithSuggestion(P1);

        var sergeant = game.First(Place.Base(P1), "vanguard-sergeant");
        Assert.True(game.State[sergeant].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Equal(MappingStatus.Full, engine.Effects.For("vanguard-sergeant").Status);
    }
}
