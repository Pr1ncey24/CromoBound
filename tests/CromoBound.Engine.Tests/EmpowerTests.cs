using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EmpowerTests
{
    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    /// <summary>Kayle's abilities: the two passives (0, 1), then the Empower keyword's activation (2).</summary>
    private const int KayleEmpower = 2;

    [Fact]
    public void Kayle_empowers_three_times_and_no_more()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified"));
        var kayle = game.Put("kayle-justified", Place.Base(P1));
        game.Runes(P1, "fury-rune", 9);
        var engine = game.Start();

        for (var i = 1; i <= 3; i++)
        {
            engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));
            engine.PayWithSuggestion(P1);
            PassBoth(engine);
            Assert.Equal(i, game.State[kayle].EmpowerCount);
            Assert.Equal(3 + 2 * i, engine.MightOf(kayle));
        }

        Assert.True(engine.Has(game.State[kayle], DisplayKeyword.Ganking));
        Assert.Equal(3, Effects.Modifiers.KeywordValue(engine, game.State[kayle], MechanicalKeyword.Deflect));
        Assert.DoesNotContain(engine.Decision<PriorityDecision>().Activations, a => a.Source == kayle);
    }

    [Fact]
    public void A_new_object_starts_unempowered()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.State[unit].EmpowerCount = 2;
        var engine = game.Start();

        var moved = game.State.Move(unit, Place.Trash(P1), DeckPosition.Top)!.Value;

        Assert.Equal(0, game.State[moved].EmpowerCount);
        Assert.False(game.State[moved].Empowered);
    }

    [Fact]
    public void Risen_altar_lowers_kayles_empower_by_one_energy()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified", "risen-altar"), firstBattlefield: "risen-altar");
        game.State.Battlefields[0].Controller = P1;
        var kayle = game.Put("kayle-justified", Place.Battlefield(0));
        game.Runes(P1, "fury-rune", 3);
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(2, cost.Energy);
        Assert.Empty(cost.Power);
    }

    [Fact]
    public void Risen_altar_asks_which_part_to_lower_when_the_cost_has_both()
    {
        const string altar = """
            { "cardId": "bf-a", "status": "Full", "abilities": [ { "kind": "Passive", "modifiers": [
              { "type": "KeywordCostReduction", "keyword": "Empower", "energy": 1, "orPower": ["Any"],
                "appliesTo": { "select": "Unit", "all": true, "filter": { "relation": "Friendly", "location": { "ref": "Here" } } } } ] } ] }
            """;
        const string unit = """
            { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Empower", "cost": { "energy": 2, "power": ["Any"] } } ] }
            """;
        var game = new TestGame(db: EngineTestDb.Create(("bf-a", altar), ("unit-3", unit)));
        game.State.Battlefields[0].Controller = P1;
        var here = game.Put("unit-3", Place.Battlefield(0));
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(here, 0));
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(false));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(2, cost.Energy);
        Assert.Empty(cost.Power);
    }

    [Fact]
    public void Away_from_risen_altar_the_cost_is_unchanged()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified", "risen-altar"), firstBattlefield: "risen-altar");
        game.State.Battlefields[0].Controller = P1;
        var kayle = game.Put("kayle-justified", Place.Base(P1));
        game.Runes(P1, "fury-rune", 3);
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));

        Assert.Equal(3, engine.Decision<PayCostDecision>().Cost.Energy);
    }
}
