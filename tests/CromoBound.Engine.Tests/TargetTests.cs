using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TargetTests
{
    internal const string KillAUnit = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private const string KillAnEnemy = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } } } ] } ] }
        """;

    private const string DealTwice = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } },
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>P1 holds "spell" (with the given effects) and one fury rune; P1 has unit-2 and P2 has unit-3 in Base unless arranged otherwise.</summary>
    private static (TestGame Game, Game Engine) Setup(string effects, bool ownUnit = true, bool enemyUnit = true)
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", effects)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        if (ownUnit) game.Put("unit-2", Place.Base(P1));
        if (enemyUnit) game.Put("unit-3", Place.Base(P2));
        return (game, game.Start());
    }

    private static ObjectId Spell(TestGame game) => game.First(Place.Hand(P1), "spell");

    [Fact]
    public void Targets_are_chosen_after_play_choices_and_before_the_cost()
    {
        var (game, engine) = Setup(KillAUnit);
        var enemy = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new PlayCard(Spell(game)));
        var choose = engine.Decision<ChooseTargetsDecision>();
        Assert.Equal((P1, 0, 1, 1), (choose.Player, choose.Slot, choose.Min, choose.Max));
        Assert.Equal(2, choose.Options.Count);
        var chosen = engine.Accept(P1, new ChooseTargets { Targets = [enemy] });

        Assert.IsType<PayCostDecision>(engine.Pending);
        Assert.Equal(new[] { enemy }, Assert.Single(game.State.Chain).Effect!.Targets[0]);
        var announced = Assert.Single(chosen.Events.OfType<TargetsChosen>());
        Assert.Equal((game.State.Chain[0].Id, 0), (announced.ItemId, announced.Slot));
        Assert.Equal(new[] { enemy }, announced.Targets);
    }

    [Fact]
    public void Invalid_target_choices_are_rejected_without_changes()
    {
        var (game, engine) = Setup(KillAUnit);
        var mine = game.First(Place.Base(P1), "unit-2");
        var enemy = game.First(Place.Base(P2), "unit-3");
        var inHand = game.First(Place.Hand(P1), "unit-2");
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [inHand] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [mine, enemy] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [enemy, enemy] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets()).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new PayCost()).Rejection!.Code);
        Assert.Empty(Assert.Single(game.State.Chain).Effect!.Targets);
        Assert.IsType<ChooseTargetsDecision>(engine.Pending);
    }

    [Fact]
    public void A_single_legal_target_is_chosen_automatically_and_announced()
    {
        var (game, engine) = Setup(KillAnEnemy);
        var enemy = game.First(Place.Base(P2), "unit-3");

        var result = engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Contains(result.Events, e => e is ChoiceMade { Kind: "Targets" } made && made.Chosen.SequenceEqual(new[] { enemy }));
        Assert.Contains(result.Events, e => e is TargetsChosen { Slot: 0 } chosen && chosen.Targets.SequenceEqual(new[] { enemy }));
        Assert.IsType<PayCostDecision>(engine.Pending);
    }

    [Fact]
    public void A_spell_without_enough_legal_targets_is_not_playable()
    {
        var (game, engine) = Setup(KillAnEnemy, enemyUnit: false);

        Assert.DoesNotContain(Spell(game), engine.Decision<PriorityDecision>().Playable);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new PlayCard(Spell(game))).Rejection!.Code);
    }

    [Fact]
    public void Each_target_slot_is_asked_in_order_and_may_pick_the_same_unit()
    {
        var (game, engine) = Setup(DealTwice);
        var enemy = game.First(Place.Base(P2), "unit-3");
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Equal(0, engine.Decision<ChooseTargetsDecision>().Slot);
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.Equal(1, engine.Decision<ChooseTargetsDecision>().Slot);
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });

        var targets = Assert.Single(game.State.Chain).Effect!.Targets;
        Assert.Equal(new[] { enemy }, targets[0]);
        Assert.Equal(new[] { enemy }, targets[1]);
    }

    [Fact]
    public void Cancelling_at_the_target_choice_returns_the_card()
    {
        var (game, engine) = Setup(KillAUnit);
        engine.Accept(P1, new PlayCard(Spell(game)));

        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_manual_action_during_the_target_choice_asks_again_with_fresh_options()
    {
        var (game, engine) = Setup(DealTwice);
        var mine = game.First(Place.Base(P1), "unit-2");
        var enemy = game.First(Place.Base(P2), "unit-3");
        engine.Accept(P1, new PlayCard(Spell(game)));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.Equal(2, engine.Decision<ChooseTargetsDecision>().Options.Count);

        var result = engine.SubmitManual(P2, new ManualMoveCard(mine, Place.Trash(P1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.IsType<PayCostDecision>(engine.Pending);
        var targets = Assert.Single(game.State.Chain).Effect!.Targets;
        Assert.Equal(new[] { enemy }, targets[0]);
        Assert.Equal(new[] { enemy }, targets[1]);
    }

    [Fact]
    public void Unmapped_spells_ask_no_targets()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.IsType<PayCostDecision>(engine.Pending);
    }

    [Fact]
    public void Target_choices_round_trip_through_json_and_a_null_list_is_rejected()
    {
        PlayerAction action = new ChooseTargets { Targets = [new ObjectId(4)] };
        var json = CromoJson.Serialize(action);
        PendingDecision decision = new ChooseTargetsDecision(P1, new ObjectId(2), 0, [new ObjectId(4)], 1, 1);
        var (game, engine) = Setup(KillAUnit);
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Contains("\"ChooseTargets\"", json);
        Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        Assert.Contains("\"ChooseTargets\"", CromoJson.Serialize(decision));
        var malformed = CromoJson.Deserialize<PlayerAction>("""{ "type": "ChooseTargets", "targets": null }""");
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, malformed).Rejection!.Code);
    }
}
