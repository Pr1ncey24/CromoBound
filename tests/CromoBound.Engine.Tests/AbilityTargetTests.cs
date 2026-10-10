using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AbilityTargetTests
{
    /// <summary>gear-1 played: deal 2 to an enemy unit. A trigger with a target.</summary>
    private const string Striker = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Triggered", "trigger": { "event": "Played", "subject": { "ref": "Self" } },
            "steps": [ { "action": "Deal", "amount": 2, "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } } } ] } ] }
        """;

    /// <summary>gear-1, exhaust: kill a unit. An activation with a target.</summary>
    private const string Slayer = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Activated", "cost": { "exhaustSelf": true },
            "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private static (TestGame Game, Rules.Game Engine) Setup(string json, Action<TestGame> arrange)
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", json)));
        arrange(game);
        return (game, game.Start());
    }

    [Fact]
    public void A_trigger_chooses_its_target_when_it_goes_on_the_chain()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("unit-2", Place.Base(P2));
            g.Put("unit-3", Place.Base(P2));
        });
        var big = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);
        var choose = engine.Decision<ChooseTargetsDecision>();
        Assert.Equal(2, choose.Options.Count);
        engine.Accept(P1, new ChooseTargets { Targets = [big] });
        Assert.Single(game.State.Chain);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(2, game.State[big].Damage);
    }

    [Fact]
    public void A_trigger_with_one_legal_target_takes_it_without_asking()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("unit-2", Place.Base(P2));
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);

        Assert.IsType<PriorityDecision>(engine.Pending);
        Assert.Single(game.State.Chain);
    }

    [Fact]
    public void A_trigger_with_no_legal_target_is_not_put_on_the_chain()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);

        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void An_activation_chooses_its_target_before_paying_and_can_be_cancelled_there()
    {
        var (game, engine) = Setup(Slayer, g =>
        {
            g.Put("gear-1", Place.Base(P1));
            g.Put("unit-2", Place.Base(P1));
            g.Put("unit-3", Place.Base(P2));
        });
        var gear = game.First(Place.Base(P1), "gear-1");
        var enemy = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new ActivateAbility(gear, 0));
        Assert.Equal(2, engine.Decision<ChooseTargetsDecision>().Options.Count);
        engine.Accept(P1, new CancelPlay());
        Assert.False(game.State[gear].Exhausted);
        Assert.Empty(game.State.Chain);

        engine.Accept(P1, new ActivateAbility(gear, 0));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.True(game.State[gear].Exhausted);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
    }

    [Fact]
    public void An_activation_without_a_legal_target_is_not_offered()
    {
        var (game, engine) = Setup(Slayer, g => g.Put("gear-1", Place.Base(P1)));

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void A_selector_that_isnt_a_slot_is_chosen_on_resolution_and_the_step_records_it()
    {
        var game = new TestGame();
        var a = game.Put("unit-2", Place.Base(P2));
        var b = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        var context = new EffectContext { Controller = P1, SourceCardId = "spell" };
        Step[] steps =
        [
            new DealStep { Amount = 0, Target = new ObjectRef { Select = SelectKind.Unit, Count = 1, Filter = new Filter { Relation = Relation.Enemy } }, Store = "aimed" },
        ];

        engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(new[] { a, b }, choose.Options);
        engine.Accept(P1, new ChooseCards { Cards = [b] });

        Assert.Equal(new[] { b }, context.Vars["aimed"].Objects);
        Assert.False(context.Vars["aimed"].Happened);
    }
}
