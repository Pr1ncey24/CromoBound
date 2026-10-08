using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class PlayTests
{
    private static (TestGame Game, Game Engine) Setup(Action<TestGame> arrange)
    {
        var game = new TestGame();
        arrange(game);
        return (game, game.Start());
    }

    [Fact]
    public void Playing_a_unit_pays_and_puts_it_exhausted_in_base()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 3); });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(3, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        var unit = game.First(Place.Base(P1), "unit-3");
        Assert.True(game.State[unit].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Equal(2, game.State.At(Place.Base(P1)).Count(id => game.State[id].CardId == "chaos-rune"));
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_unit_may_enter_at_a_battlefield_you_control()
    {
        var (game, engine) = Setup(g =>
        {
            g.State.Battlefields[0].Controller = P1;
            g.Put("unit-2", Place.Battlefield(0));
            g.Put("ganker-2", Place.Hand(P1));
            g.Runes(P1, "chaos-rune", 2);
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "ganker-2")));
        var choices = engine.Decision<PlayChoicesDecision>();
        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(0) }, choices.Locations);
        Assert.Equal(RejectionCode.IllegalLocation, engine.Submit(P1, new ChoosePlayOptions(Place.Battlefield(1), false)).Rejection!.Code);

        engine.Accept(P1, new ChoosePlayOptions(Place.Battlefield(0), false));
        engine.PayWithSuggestion(P1);

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "ganker-2");
    }

    [Fact]
    public void Insufficient_payment_is_rejected_and_nothing_changes()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "fury-rune", 3); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        var runes = game.State.At(Place.Base(P1)).ToList();

        var result = engine.Submit(P1, new PayCost { Exhaust = runes });

        Assert.Equal(RejectionCode.InsufficientPayment, result.Rejection!.Code);
        Assert.All(runes, id => Assert.False(game.State[id].Exhausted));
        Assert.Null(engine.Decision<PayCostDecision>().Suggested);
    }

    [Fact]
    public void An_exhausted_or_foreign_rune_cannot_pay()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Runes(P2, "fury-rune", 1);
        });
        var mine = game.State.At(Place.Base(P1))[0];
        var theirs = game.State.At(Place.Base(P2))[0];
        engine.Accept(P1, new UseRune(mine, RuneUse.Exhaust));
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Equal(RejectionCode.InsufficientPayment, engine.Submit(P1, new PayCost { Exhaust = [mine] }).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.Submit(P1, new PayCost { Exhaust = [theirs] }).Rejection!.Code);

        engine.Accept(P1, new PayCost());
        Assert.Equal(0, game.State.Player(P1).Pool.Energy);
    }

    [Fact]
    public void Cancelling_returns_the_card_and_spends_nothing()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 3); });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "unit-3");
        Assert.Empty(game.State.Chain);
        Assert.All(game.State.At(Place.Base(P1)), id => Assert.False(game.State[id].Exhausted));
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Adjusting_the_cost_asks_again_with_the_new_cost()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 1); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));

        engine.Accept(P1, new AdjustCost { Energy = -3 });

        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(0, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        Assert.Contains(game.State.At(Place.Base(P1)), id => game.State[id].CardId == "unit-3");
    }

    [Fact]
    public void Accelerate_adds_its_cost_and_the_unit_enters_ready()
    {
        var (game, engine) = Setup(g => { g.Put("accel-3", Place.Hand(P1)); g.Runes(P1, "fury-rune", 4); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "accel-3")));

        Assert.True(engine.Decision<PlayChoicesDecision>().AccelerateAvailable);
        engine.Accept(P1, new ChoosePlayOptions(Place.Base(P1), true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(4, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        Assert.False(game.State[game.First(Place.Base(P1), "accel-3")].Exhausted);
    }

    [Fact]
    public void A_spell_waits_on_the_chain_and_is_resolved_by_hand()
    {
        var (game, engine) = Setup(g => { g.Put("spell", Place.Hand(P1)); g.Runes(P1, "fury-rune", 1); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Assert.Equal(ChainItemStatus.Finalized, Assert.Single(game.State.Chain).Status);
        Assert.True(engine.Decision<PriorityDecision>().CanPass);

        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal((P1, "spell"), (resolve.Player, resolve.CardId));

        engine.Accept(P1, new ResolveDone());

        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.False(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void A_player_can_stack_reactions_on_their_own_spell_before_passing()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Put("spell", Place.Hand(P1));
            g.Put("reaction-spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 2);
        });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        var options = engine.Decision<PriorityDecision>();
        var reaction = game.First(Place.Hand(P1), "reaction-spell");
        Assert.Equal(P1, options.Player);
        Assert.Contains(reaction, options.Playable);
        Assert.DoesNotContain(game.First(Place.Hand(P1), "spell"), options.Playable);

        engine.Accept(P1, new PlayCard(reaction));
        engine.PayWithSuggestion(P1);
        Assert.Equal(2, game.State.Chain.Count);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        Assert.Equal(game.State.Chain[1].Id, engine.Decision<ResolveManuallyDecision>().ChainItem);

        engine.Accept(P1, new ResolveDone());
        Assert.Single(game.State.Chain);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void The_opponent_can_respond_and_their_reaction_resolves_first()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("reaction-spell", Place.Hand(P2));
            g.Runes(P2, "fury-rune", 1);
        });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());

        engine.Accept(P2, new PlayCard(game.First(Place.Hand(P2), "reaction-spell")));
        engine.PayWithSuggestion(P2);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new Pass());

        Assert.Equal(P2, engine.Decision<ResolveManuallyDecision>().Player);
    }
}
