using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class LocationTests
{
    /// <summary>P1 holds Soulspinner (Ambush) and the unmapped "spell", with four runes. With <paramref name="unitThere"/>, P1
    /// controls the first battlefield and has a unit there.</summary>
    private static TestGame SoulspinnerGame(bool unitThere)
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soulspinner"));
        if (unitThere)
        {
            game.State.Battlefields[0].Controller = P1;
            game.Put("unit-2", Place.Battlefield(0));
        }
        game.Put("soulspinner", Place.Hand(P1));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 4);
        return game;
    }

    [Fact]
    public void Ambush_lets_a_unit_be_played_as_a_reaction_to_a_battlefield_where_you_have_units()
    {
        var game = SoulspinnerGame(unitThere: true);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        var soulspinner = game.First(Place.Hand(P1), "soulspinner");
        Assert.Contains(soulspinner, engine.Decision<PriorityDecision>().Playable);
        engine.Accept(P1, new PlayCard(soulspinner));
        engine.PayWithSuggestion(P1);

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "soulspinner");
        Assert.Single(game.State.Chain);
    }

    [Fact]
    public void Without_units_on_a_battlefield_ambush_gives_no_reaction_timing()
    {
        var game = SoulspinnerGame(unitThere: false);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Assert.DoesNotContain(game.First(Place.Hand(P1), "soulspinner"), engine.Decision<PriorityDecision>().Playable);
    }

    [Fact]
    public void In_your_main_phase_an_ambush_unit_may_still_enter_at_your_base()
    {
        var game = SoulspinnerGame(unitThere: true);
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "soulspinner")));

        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(0) }, engine.Decision<PlayChoicesDecision>().Locations);
    }

    [Fact]
    public void Rengar_trophy_hunter_can_be_played_to_a_battlefield_with_enemy_units()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("rengar-trophy-hunter"));
        game.State.Battlefields[1].Controller = P2;
        game.Put("unit-2", Place.Battlefield(1), P2);
        game.Put("rengar-trophy-hunter", Place.Hand(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rengar-trophy-hunter")));

        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(1) }, engine.Decision<PlayChoicesDecision>().Locations);
    }

    [Fact]
    public void An_ordinary_unit_isnt_offered_a_battlefield_with_enemy_units()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        game.Put("unit-2", Place.Battlefield(1), P2);
        game.Put("unit-3", Place.Hand(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));

        Assert.IsType<PayCostDecision>(engine.Pending);
    }
}
