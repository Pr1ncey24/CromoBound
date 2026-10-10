using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ActivationTests
{
    /// <summary>gear-1 with a Reaction ability (exhaust it: draw 1) and a default-timing one (draw 1).</summary>
    private const string TwoAbilities = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Activated", "timing": "Reaction", "cost": { "exhaustSelf": true }, "steps": [ { "action": "Draw", "amount": 1 } ] },
          { "kind": "Activated", "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
        """;

    /// <summary>Garbage Grabber in P1's Base, one rune to pay with, and <paramref name="trash"/> cards in P1's trash.</summary>
    private static (TestGame Test, Game Engine, ObjectId Grabber) Grabber(int trash)
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("garbage-grabber"));
        var grabber = game.Put("garbage-grabber", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        for (var i = 0; i < trash; i++) game.Put("unit-2", Place.Trash(P1));
        return (game, game.Start(), grabber);
    }

    [Fact]
    public void Garbage_grabber_is_offered_only_with_three_cards_in_your_trash()
    {
        var (game, engine, grabber) = Grabber(trash: 2);
        Assert.Empty(engine.Decision<PriorityDecision>().Activations);

        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(game.State.At(Place.Hand(P1))[0], Place.Trash(P1))).Accepted);

        Assert.Equal(new[] { new ActivateOption(grabber, 0) }, engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Garbage_grabber_recycles_three_pays_one_exhausts_and_draws_one()
    {
        var (game, engine, grabber) = Grabber(trash: 3);
        var deck = game.State.At(Place.MainDeck(P1)).Count;
        var hand = game.State.At(Place.Hand(P1)).Count;

        var started = engine.Accept(P1, new ActivateAbility(grabber, 0));

        Assert.Contains(started.Events, e => e is ChoiceMade { Kind: "Cards" });
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        var paid = engine.PayWithSuggestion(P1);
        Assert.Contains(paid.Events, e => e is AbilityActivated { Ability: 0 });
        Assert.True(game.State[grabber].Exhausted);
        Assert.Empty(game.State.At(Place.Trash(P1)));
        Assert.Equal(deck + 3, game.State.At(Place.MainDeck(P1)).Count);
        var item = Assert.Single(game.State.Chain);
        Assert.Equal(AbilityKind.Activated, item.AbilityKind);
        Assert.Equal("garbage-grabber", item.SourceCardId);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void With_four_cards_in_the_trash_the_player_chooses_which_three_to_recycle()
    {
        var (game, engine, grabber) = Grabber(trash: 4);
        var trash = game.State.At(Place.Trash(P1)).ToList();

        engine.Accept(P1, new ActivateAbility(grabber, 0));
        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((3, 3, 4), (choose.Min, choose.Max, choose.Options.Count));
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseCards { Cards = [trash[0], trash[1]] }).Rejection!.Code);
        engine.Accept(P1, new ChooseCards { Cards = [trash[0], trash[1], trash[2]] });
        engine.PayWithSuggestion(P1);

        Assert.Equal(new[] { trash[3] }, game.State.At(Place.Trash(P1)));
    }

    [Fact]
    public void Cancelling_at_the_choice_or_at_the_payment_changes_nothing()
    {
        var (game, engine, grabber) = Grabber(trash: 4);
        var trash = game.State.At(Place.Trash(P1)).ToList();

        engine.Accept(P1, new ActivateAbility(grabber, 0));
        engine.Accept(P1, new CancelPlay());
        engine.Accept(P1, new ActivateAbility(grabber, 0));
        engine.Accept(P1, new ChooseCards { Cards = [trash[0], trash[1], trash[2]] });
        engine.Decision<PayCostDecision>();
        engine.Accept(P1, new CancelPlay());

        Assert.Equal(trash, game.State.At(Place.Trash(P1)));
        Assert.False(game.State[grabber].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Single(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void An_exhausted_source_cant_activate_an_ability_that_exhausts_it()
    {
        var (_, engine, grabber) = Grabber(trash: 3);
        Assert.True(engine.SubmitManual(P1, new ManualSetStatus(grabber, StatusKind.Exhausted, true)).Accepted);

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new ActivateAbility(grabber, 0)).Rejection!.Code);
    }

    [Fact]
    public void Reaction_abilities_are_offered_on_a_chain_and_default_ones_only_in_your_neutral_open_main()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", TwoAbilities)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        Assert.Equal(new[] { new ActivateOption(gear, 0), new ActivateOption(gear, 1) }, engine.Decision<PriorityDecision>().Activations);

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        Assert.Equal(new[] { new ActivateOption(gear, 0) }, engine.Decision<PriorityDecision>().Activations);

        engine.Accept(P1, new ActivateAbility(gear, 0));

        Assert.True(game.State[gear].Exhausted);
        Assert.Equal(2, game.State.Chain.Count);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Kharox_empowers_burns_the_opponent_and_may_play_a_unit_from_their_trash()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kharox"));
        var kharox = game.Put("kharox", Place.Base(P1));
        game.Runes(P1, "chaos-rune", 8);
        var engine = game.Start();

        Assert.Equal(new[] { new ActivateOption(kharox, 1) }, engine.Decision<PriorityDecision>().Activations);
        engine.Accept(P1, new ActivateAbility(kharox, 1));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.True(game.State[kharox].Empowered);
        Assert.Equal(AbilityKind.Triggered, Assert.Single(game.State.Chain).AbilityKind);
        engine.Accept(P1, new Pass());
        var burned = engine.Accept(P2, new Pass());

        Assert.Contains(burned.Events, e => e is PlayerChosen { Chosen.Index: 1 });
        Assert.Equal(3, game.State.At(Place.Trash(P2)).Count);
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Single(game.State.Chain);
        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(3, choose.Options.Count);
        engine.Accept(P1, new ChooseCards { Cards = [choose.Options[0]] });

        var played = game.First(Place.Base(P1), "unit-2");
        Assert.Equal(P1, game.State[played].Controller);
        Assert.Equal(P2, game.State[played].Owner);
        Assert.Equal(2, game.State.At(Place.Trash(P2)).Count);
        Assert.Empty(game.State.Chain);
        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Runes_offer_no_activations_and_keep_use_rune()
    {
        var game = new TestGame(db: EngineTestDb.Create(("fury-rune", """
            { "cardId": "fury-rune", "status": "Full", "abilities": [
              { "kind": "Activated", "timing": "Reaction", "cost": { "exhaustSelf": true }, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
            """)));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        var priority = engine.Decision<PriorityDecision>();

        Assert.Empty(priority.Activations);
        Assert.Single(priority.Runes);
    }

    [Fact]
    public void Fury_runes_file_says_what_use_rune_does()
    {
        var real = CardRepository.Load(RepoPaths.Data);
        var file = real.Effects["fury-rune"].File;

        Assert.Equal(2, file.Abilities.Count);
        var exhaust = Assert.IsType<ActivatedAbility>(file.Abilities[0]);
        Assert.Equal(Timing.Reaction, exhaust.Timing);
        Assert.True(exhaust.Cost?.ExhaustSelf);
        Assert.Equal(1, Assert.IsType<AddStep>(Assert.Single(exhaust.Steps)).Energy?.Literal);
        var recycle = Assert.IsType<ActivatedAbility>(file.Abilities[1]);
        Assert.Equal(Timing.Reaction, recycle.Timing);
        Assert.Equal(RefKind.Self, Assert.IsType<RecycleStep>(Assert.Single(recycle.Cost!.Actions)).Target?.Ref);
        Assert.Equal(new[] { PowerSymbol.Fury }, Assert.IsType<AddStep>(Assert.Single(recycle.Steps)).Power);
        Assert.Equal(Domain.Fury, Assert.Single(real.Cards["fury-rune"].Domains));
        var info = new CardEffects(real).For("fury-rune");
        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Empty(info.Abilities);
    }

    /// <summary>gear-1 with a default-timing ability whose cost is spending 2 XP: draw 1.</summary>
    private const string SpendsXp = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Activated", "cost": { "actions": [ { "action": "SpendXp", "amount": 2 } ] }, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
        """;

    [Fact]
    public void A_spend_xp_cost_needs_the_xp_and_lowers_it_when_paid()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", SpendsXp)));
        var gear = game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();
        Assert.Empty(engine.Decision<PriorityDecision>().Activations);

        Assert.True(engine.SubmitManual(P1, new ManualAdjustXp(P1, 3)).Accepted);
        Assert.Equal(new[] { new ActivateOption(gear, 0) }, engine.Decision<PriorityDecision>().Activations);

        var started = engine.Accept(P1, new ActivateAbility(gear, 0));

        Assert.Contains(started.Events, e => e is XpChanged { Xp: 1 });
        Assert.Equal(1, game.State.Player(P1).Xp);
        Assert.Contains(started.Events, e => e is AbilityActivated { Ability: 0 });
    }
}
