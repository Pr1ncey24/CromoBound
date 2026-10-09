using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class CostTests
{
    private const string DeflectUnit = """{ "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Deflect" } ] }""";

    private const string DealTwice = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } },
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>P1 holds Noxus Hopeful and a unit-2, with six runes.</summary>
    private static TestGame HopefulGame()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("noxus-hopeful"));
        game.Put("noxus-hopeful", Place.Hand(P1));
        game.Put("unit-2", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 6);
        return game;
    }

    [Fact]
    public void Noxus_hopeful_costs_2_less_after_you_played_another_card_this_turn()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));
        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
        engine.Accept(P1, new CancelPlay());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(2, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void A_cancelled_play_doesnt_count_for_legion()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.Accept(P1, new CancelPlay());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void Legion_counts_only_this_turns_plays()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new EndTurn());
        engine.Accept(P2, new EndTurn());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void Deflect_taxes_an_opponent_who_targets_it()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(1, cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Any }, cost.Power);
    }

    [Fact]
    public void Deflect_doesnt_tax_targeting_your_own_unit()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Empty(engine.Decision<PayCostDecision>().Cost.Power);
    }

    [Fact]
    public void Deflect_taxes_each_choice_of_an_enemy_target()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", DealTwice), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Equal(new[] { PowerSymbol.Any, PowerSymbol.Any }, engine.Decision<PayCostDecision>().Cost.Power);
    }
}
