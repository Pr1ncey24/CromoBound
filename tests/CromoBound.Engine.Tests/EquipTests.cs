using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EquipTests
{
    private const string EquipGear = """
        { "cardId": "gear-1", "status": "Full", "keywords": [ { "keyword": "Equip", "cost": { "energy": 1, "power": ["Fury"] } } ] }
        """;

    private const string WeaponmasterUnit = """{ "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Weaponmaster" } ] }""";

    [Fact]
    public void Equip_attaches_the_gear_to_your_only_unit()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        Assert.Contains(new ActivateOption(gear, 0), engine.Decision<PriorityDecision>().Activations);
        engine.Accept(P1, new ActivateAbility(gear, 0));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Fury }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(unit, game.State[gear].AttachedTo);
        Assert.Equal(Place.Battlefield(0), game.State[gear].Place);
    }

    [Fact]
    public void With_several_units_the_player_chooses_which_to_equip()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-2", Place.Base(P1));
        var second = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();
        engine.Accept(P1, new ActivateAbility(gear, 0));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((2, 1, 1), (choose.Options.Count, choose.Min, choose.Max));
        engine.Accept(P1, new ChooseCards { Cards = [second] });

        Assert.Equal(second, game.State[gear].AttachedTo);
    }

    [Fact]
    public void Weaponmaster_may_attach_your_equipment_for_its_equip_cost_minus_a_power_symbol()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear), ("unit-3", WeaponmasterUnit)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-3", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 5);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((0, 1), (choose.Min, choose.Max));
        Assert.Equal(new[] { gear }, choose.Options);
        engine.Accept(P1, new ChooseCards { Cards = [gear] });
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        Assert.Equal(game.First(Place.Base(P1), "unit-3"), game.State[gear].AttachedTo);
    }

    [Fact]
    public void Declining_weaponmaster_attaches_nothing()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear), ("unit-3", WeaponmasterUnit)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-3", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 5);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        engine.Accept(P1, new ChooseCards { Cards = [] });

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Veteran_poro_offers_no_equipment_whose_equip_cost_is_unknown()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("veteran-poro"));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("veteran-poro", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "veteran-poro")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }
}
