using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class HiddenTests
{
    /// <summary>P1 controls battlefield 0 (with a unit there), holds hidden-unit, and has one chaos rune.</summary>
    private static (TestGame Game, Game Engine, ObjectId Card) Setup()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        game.Put("hidden-unit", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 1);
        var engine = game.Start();
        return (game, engine, game.First(Place.Hand(P1), "hidden-unit"));
    }

    [Fact]
    public void Hiding_pays_any_power_and_puts_the_card_face_down_privately()
    {
        var (game, engine, card) = Setup();
        var hide = Assert.Single(engine.Decision<PriorityDecision>().Hides);
        Assert.Equal(new[] { 0 }, hide.Battlefields);

        engine.Accept(P1, new Hide(card, 0));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(new[] { PowerSymbol.Any }, pay.Cost.Power);
        var result = engine.PayWithSuggestion(P1);

        var hidden = game.State[Assert.Single(game.State.At(Place.Facedown(0)))];
        Assert.True(hidden.Facedown);
        Assert.Equal(P1, hidden.Controller);
        Assert.Contains(result.Events, e => e is CardMoved { CardId: null, VisibleTo: null, ToPlace.Kind: PlaceKind.Facedown });
        Assert.Contains(result.Events, e => e is CardMoved { CardId: "hidden-unit", VisibleTo: { Index: 0 } });
    }

    [Fact]
    public void A_hidden_card_is_playable_from_the_next_turn_ignoring_its_base_cost()
    {
        var (game, engine, card) = Setup();
        engine.Accept(P1, new Hide(card, 0));
        engine.PayWithSuggestion(P1);
        var facedown = Assert.Single(game.State.At(Place.Facedown(0)));
        Assert.DoesNotContain(facedown, engine.Decision<PriorityDecision>().Playable);

        engine.Accept(P1, new EndTurn());
        engine.Accept(P2, new EndTurn());
        Assert.Contains(facedown, engine.Decision<PriorityDecision>().Playable);

        engine.Accept(P1, new PlayCard(facedown));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(0, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        engine.Accept(P1, new PayCost());

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "hidden-unit");
        Assert.Empty(game.State.At(Place.Facedown(0)));
    }

    [Fact]
    public void A_facedown_card_is_trashed_when_control_of_its_battlefield_is_lost()
    {
        var (game, engine, card) = Setup();
        engine.Accept(P1, new Hide(card, 0));
        engine.PayWithSuggestion(P1);
        var unit = game.First(Place.Battlefield(0), "unit-2");

        engine.Accept(P1, new StandardMove { Units = [unit], Destination = Place.Base(P1) });

        Assert.Empty(game.State.At(Place.Facedown(0)));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "hidden-unit");
    }

    [Fact]
    public void Hide_is_not_offered_without_a_controlled_battlefield()
    {
        var game = new TestGame();
        game.Put("hidden-unit", Place.Hand(P1));

        var engine = game.Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Hides);
    }
}
