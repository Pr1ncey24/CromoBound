using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ManualTests
{
    private static SubmitResult Manual(Game game, PlayerId player, ManualAction action)
    {
        var result = game.SubmitManual(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void Manual_damage_kills_through_cleanup_and_keeps_the_decision()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        var result = Manual(engine, P2, new ManualDamage(unit, 2));

        Assert.False(game.State.Exists(unit));
        Assert.Contains(result.Events, e => e is ManualActionTaken { Action: ManualDamage });
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Manual_damage_during_a_turn_point_is_cleaned_up_right_away()
    {
        var game = new TestGame();
        game.Put("dawn-relic", Place.Base(P1));
        var enemy = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();
        Assert.IsType<TurnPointDecision>(engine.Pending);

        Manual(engine, P1, new ManualDamage(enemy, 2));

        Assert.False(game.State.Exists(enemy));
        Assert.Equal(P1, engine.Decision<TurnPointDecision>().Player);
    }

    [Fact]
    public void Manual_actions_are_allowed_while_resolving_by_hand()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var discard = game.Put("unit-3", Place.Hand(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Manual(engine, P2, new ManualMoveCard(discard, Place.Trash(P2)));

        Assert.IsType<ResolveManuallyDecision>(engine.Pending);
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        engine.Accept(P1, new ResolveDone());
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Moving_a_unit_to_a_battlefield_contests_it()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P1, new ManualMoveCard(unit, Place.Battlefield(1)));

        Assert.Equal(1, game.State.Showdown!.Battlefield);
        Assert.True(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void Tokens_points_xp_pool_and_statuses_can_be_set_by_hand()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P1, new ManualCreateToken("token-recruit", Place.Base(P1), P1));
        Manual(engine, P1, new ManualAdjustPoints(P1, 3));
        Manual(engine, P1, new ManualAdjustXp(P1, 2));
        Manual(engine, P1, new ManualAdjustPool(P1, 2, Models.Cards.Domain.Fury, 1));
        Manual(engine, P1, new ManualSetStatus(unit, StatusKind.Stunned, true));
        Manual(engine, P1, new ManualModifyMight(unit, 2, Duration.ThisTurn));

        Assert.Contains(game.State.At(Place.Base(P1)), id => game.State[id].IsToken);
        Assert.Equal(3, game.State.Player(P1).Points);
        Assert.Equal(2, game.State.Player(P1).Xp);
        Assert.Equal(2, game.State.Player(P1).Pool.Energy);
        Assert.Equal(1, game.State.Player(P1).Pool.Power[Models.Cards.Domain.Fury]);
        Assert.True(game.State[unit].Stunned);
        Assert.Equal(4, engine.MightOf(unit));
    }

    [Fact]
    public void Looking_at_the_top_is_private_and_revealing_is_public()
    {
        var game = new TestGame();
        var engine = game.Start();

        var look = Manual(engine, P1, new ManualLookAtTop(P1, PlaceKind.MainDeck, 2));
        var reveal = Manual(engine, P1, new ManualReveal(game.State.At(Place.Hand(P1))[0]));

        Assert.Contains(look.Events, e => e is CardsLookedAt { VisibleTo: { Index: 0 }, CardIds.Count: 2 });
        Assert.Contains(look.Events, e => e is CardsLookedAt { VisibleTo: null, CardIds: null, Count: 2 });
        Assert.Contains(reveal.Events, e => e is CardRevealed { CardId: "unit-2", VisibleTo: null });
    }

    [Fact]
    public void Countering_removes_a_finalized_item_and_trashes_its_card()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Manual(engine, P2, new ManualCounter(game.State.Chain[0].Id));

        Assert.Empty(game.State.Chain);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.False(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void An_ability_added_by_hand_goes_on_the_chain_and_is_resolved_by_hand()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P2, new AddAbilityToChain(unit, 1, AbilityKind.Triggered));

        var item = Assert.Single(game.State.Chain);
        Assert.Equal((P1, ChainItemStatus.Finalized), (item.Controller, item.Status));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal(("unit-2", "unit-2"), (resolve.CardId, resolve.Text));
        engine.Accept(P1, new ResolveDone());
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void An_ability_with_a_cost_asks_its_controller_to_pay()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        Manual(engine, P1, new AddAbilityToChain(unit, 1, AbilityKind.Activated) { Cost = new TotalCost(1, []) });

        Assert.Equal(P1, engine.Decision<PayCostDecision>().Player);
        engine.PayWithSuggestion(P1);
        Assert.Equal(ChainItemStatus.Finalized, Assert.Single(game.State.Chain).Status);
    }

    [Fact]
    public void A_trigger_added_during_a_turn_point_resolves_before_the_turn_goes_on()
    {
        var game = new TestGame();
        var relic = game.Put("dawn-relic", Place.Base(P1));
        var engine = game.Start();
        Assert.IsType<TurnPointDecision>(engine.Pending);

        Manual(engine, P1, new AddAbilityToChain(relic, 1, AbilityKind.Triggered));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new ResolveDone());

        Assert.Equal(TurnPoint.StartOfBeginning, engine.Decision<TurnPointDecision>().Point);
        engine.Accept(P1, new ContinueTurn());
        Assert.Equal(Phase.Main, game.State.Turn.Phase);
    }

    [Fact]
    public void Invalid_manual_actions_are_rejected()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(RejectionCode.IllegalLocation, engine.SubmitManual(P1, new ManualMoveCard(unit, Place.Chain)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualDamage(new ObjectId(999), 1)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.SubmitManual(P1, new AddAbilityToChain(unit, 5, AbilityKind.Triggered)).Rejection!.Code);
    }

    [Fact]
    public void Moving_an_assigned_defender_away_mid_combat_does_not_break_the_game()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var attacker = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();
        engine.Accept(P1, new StandardMove { Units = [attacker], Destination = Place.Battlefield(1) });
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 3)] });
        Assert.Equal(P2, engine.Decision<AssignDamageDecision>().Player);

        Manual(engine, P2, new ManualMoveCard(defender, Place.Trash(P2)));

        if (engine.Pending is AssignDamageDecision) engine.Accept(P2, new AssignDamage { Assignments = [new(attacker, 2)] });
        Assert.IsNotType<AssignDamageDecision>(engine.Pending);
        Assert.NotNull(engine.Pending);
    }

    [Fact]
    public void Moving_the_card_being_hidden_cancels_the_hide()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        var card = game.Put("hidden-unit", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 1);
        var rune = game.First(Place.Base(P1), "chaos-rune");
        var engine = game.Start();
        engine.Accept(P1, new Hide(card, 0));
        Assert.IsType<PayCostDecision>(engine.Pending);

        Manual(engine, P1, new ManualMoveCard(card, Place.Trash(P1)));

        Assert.IsType<PriorityDecision>(engine.Pending);
        Assert.Empty(game.State.At(Place.Facedown(0)));
        Assert.False(game.State[rune].Exhausted);
    }

    [Fact]
    public void A_manual_action_during_a_showdown_choice_reruns_cleanup()
    {
        var game = new TestGame();
        game.State.Battlefields[0].ContestedBy = P1;
        game.State.Battlefields[1].ContestedBy = P1;
        var first = game.Put("unit-2", Place.Battlefield(0));
        game.Put("unit-2", Place.Battlefield(1));
        var engine = game.Start();
        Assert.IsType<ChooseShowdownDecision>(engine.Pending);

        Manual(engine, P1, new ManualMoveCard(first, Place.Trash(P1)));

        Assert.Equal(1, game.State.Showdown!.Battlefield);
        Assert.DoesNotContain(0, game.State.StagedShowdowns);
    }

    [Fact]
    public void An_ability_cost_without_a_power_list_is_rejected_before_anything_changes()
    {
        var game = new TestGame();
        var relic = game.Put("dawn-relic", Place.Base(P1));
        var engine = game.Start();
        var pending = engine.Pending;
        var json = $$$"""{"type":"AddAbilityToChain","source":{"value":{{{relic.Value}}}},"line":1,"kind":"Triggered","cost":{"energy":1}}""";
        var action = CromoJson.Deserialize<PlayerAction>(json)!;

        var result = engine.SubmitManual(P1, (ManualAction)action);

        Assert.False(result.Accepted);
        Assert.Equal(RejectionCode.UnexpectedAction, result.Rejection!.Code);
        Assert.Empty(game.State.Chain);
        Assert.Same(pending, engine.Pending);
    }

    [Fact]
    public void A_manual_action_from_a_seat_that_does_not_exist_is_rejected()
    {
        var game = new TestGame();
        var engine = game.Start();

        var result = engine.SubmitManual(new PlayerId(9), new ManualAdjustXp(P1, 1));

        Assert.Equal(RejectionCode.NotYourDecision, result.Rejection!.Code);
        Assert.Equal(0, game.State.Player(P1).Xp);
    }

    [Fact]
    public void A_move_to_a_place_with_the_wrong_shape_is_rejected_and_the_card_stays_put()
    {
        var game = new TestGame();
        var card = game.Put("unit-2", Place.Hand(P1));
        var engine = game.Start();
        Place[] ghosts =
        [
            new(PlaceKind.Battlefield, P1, 0), new(PlaceKind.Hand, P1, 3), new(PlaceKind.Base, P1, 0),
            new(PlaceKind.Base, null, null), new(PlaceKind.Facedown, P1, 0), new(PlaceKind.Chain, P1, null),
        ];

        foreach (var ghost in ghosts)
        {
            var result = engine.SubmitManual(P1, new ManualMoveCard(card, ghost));
            Assert.Equal(RejectionCode.IllegalLocation, result.Rejection?.Code);
        }
        Assert.Equal(Place.Hand(P1), game.State[card].Place);
    }

    [Fact]
    public void A_token_at_a_place_with_the_wrong_shape_is_rejected()
    {
        var game = new TestGame();
        var engine = game.Start();

        var withPlayer = engine.SubmitManual(P1, new ManualCreateToken("token-recruit", new Place(PlaceKind.Battlefield, P1, 0), P1));
        var withIndex = engine.SubmitManual(P1, new ManualCreateToken("token-recruit", new Place(PlaceKind.Base, P1, 2), P1));

        Assert.Equal(RejectionCode.IllegalLocation, withPlayer.Rejection?.Code);
        Assert.Equal(RejectionCode.IllegalLocation, withIndex.Rejection?.Code);
        Assert.DoesNotContain(game.State.Objects, o => o.IsToken);
    }

    [Fact]
    public void A_full_facedown_slot_refuses_another_card_but_accepts_its_own()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        var hidden = game.Put("unit-2", Place.Facedown(0), P1);
        var other = game.Put("unit-2", Place.Hand(P1));
        var engine = game.Start();

        var full = engine.SubmitManual(P1, new ManualMoveCard(other, Place.Facedown(0)));
        var same = engine.SubmitManual(P1, new ManualMoveCard(hidden, Place.Facedown(0)));

        Assert.Equal(RejectionCode.IllegalLocation, full.Rejection?.Code);
        Assert.True(same.Accepted, same.Rejection?.Message);
        Assert.Equal(Place.Hand(P1), game.State[other].Place);
    }
}
