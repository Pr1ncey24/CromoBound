using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AdditionalCostTests
{
    private static TestGame Real(params string[] cardIds) => new(db: EngineTestDb.WithRealCards(cardIds));

    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    [Fact]
    public void Rampage_with_the_extra_body_gives_two_might_and_the_units_hit_each_other()
    {
        var game = Real("rampage", "body-rune");
        game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Runes(P1, "body-rune", 1);
        var friend = game.Put("unit-3", Place.Base(P1));
        var foe = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rampage")));
        engine.Accept(P1, new ChooseOptional(true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(3, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Body }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(5, engine.MightOf(friend));
        Assert.Equal(2, game.State[friend].Damage);
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-2");
        Assert.False(game.State.Exists(foe) && game.State[foe].Place.IsLocation);
    }

    [Fact]
    public void Rampage_without_the_extra_cost_uses_the_printed_mights()
    {
        var game = Real("rampage");
        game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Put("unit-2", Place.Base(P1));
        var foe = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rampage")));
        engine.Accept(P1, new ChooseOptional(false));
        Assert.Empty(engine.Decision<PayCostDecision>().Cost.Power);
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(2, game.State[foe].Damage);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "unit-2");
    }

    [Fact]
    public void Cancelling_rampage_after_choosing_the_extra_cost_spends_nothing()
    {
        var game = Real("rampage", "body-rune");
        var card = game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "body-rune", 4);
        game.Put("unit-2", Place.Base(P1));
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(card));
        engine.Accept(P1, new ChooseOptional(true));
        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "rampage");
        Assert.All(game.State.At(Place.Base(P1)).Where(id => game.State[id].CardId == "body-rune"), id => Assert.False(game.State[id].Exhausted));
        Assert.Equal(4, game.State.At(Place.Base(P1)).Count(id => game.State[id].CardId == "body-rune"));
    }

    [Fact]
    public void Sacrifice_kills_a_friendly_mighty_unit_then_draws_two_and_channels_one_exhausted()
    {
        var game = Real("sacrifice");
        game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        for (var i = 0; i < 3; i++) game.Put("fury-rune", Place.RuneDeck(P1));
        var big = game.Put("unit-3", Place.Base(P1));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "sacrifice")));
        engine.PayWithSuggestion(P1);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "unit-3");
        PassBoth(engine);

        Assert.Equal(hand - 1 + 2, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.At(Place.RuneDeck(P1)));
        var runes = game.State.At(Place.Base(P1)).Where(id => game.State[id].CardId == "fury-rune").ToList();
        Assert.Equal(4, runes.Count);
        Assert.Equal(2, runes.Count(id => game.State[id].Exhausted));
    }

    [Fact]
    public void Sacrifice_needs_a_friendly_mighty_unit()
    {
        var game = Real("sacrifice");
        var card = game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        Assert.DoesNotContain(card, engine.Decision<PriorityDecision>().Playable);
    }

    [Fact]
    public void Cancelling_sacrifice_before_paying_kills_nothing()
    {
        var game = Real("sacrifice");
        var card = game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var a = game.Put("unit-3", Place.Base(P1));
        var b = game.Put("unit-3", Place.Base(P1));
        foreach (var unit in new[] { a, b }) game.State[unit].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(card));
        Assert.Equal(new[] { a, b }, engine.Decision<ChooseCardsDecision>().Options);
        engine.Accept(P1, new ChooseCards { Cards = [a] });
        engine.Accept(P1, new CancelPlay());

        Assert.True(game.State[a].Place.IsLocation);
        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "sacrifice");
    }
}
