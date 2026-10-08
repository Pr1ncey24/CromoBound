using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
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
}
