using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

/// <summary>The Fiora sample deck's cards, each played with its real card data and effects file (docs/effects-fiora.md §5).</summary>
public class FioraDeckTests
{
    private static TestGame Real(params string[] cardIds) => new(db: EngineTestDb.WithRealCards(cardIds));

    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    [Fact]
    public void Punch_first_gives_a_unit_five_might_this_turn_only()
    {
        var game = Real("punch-first", "body-rune");
        game.Put("punch-first", Place.Hand(P1));
        game.Runes(P1, "body-rune", 3);
        var unit = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "punch-first")));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(7, engine.MightOf(unit));
        engine.Accept(P1, new EndTurn());
        Assert.Equal(2, engine.MightOf(unit));
    }

    [Fact]
    public void Divining_shells_kills_itself_to_give_a_unit_two_might()
    {
        var game = Real("divining-shells");
        var shells = game.Put("divining-shells", Place.Base(P1));
        var mine = game.Put("unit-2", Place.Base(P1));
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(shells, 0));
        engine.Accept(P1, new ChooseTargets { Targets = [mine] });
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "divining-shells");
        PassBoth(engine);

        Assert.Equal(4, engine.MightOf(mine));
    }

    [Fact]
    public void Divining_shells_needs_a_unit_to_target()
    {
        var game = Real("divining-shells");
        game.Put("divining-shells", Place.Base(P1));
        var engine = game.Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Harnessed_dragon_kills_the_enemy_unit_its_player_picks()
    {
        var game = Real("harnessed-dragon", "order-rune");
        game.Put("harnessed-dragon", Place.Hand(P1));
        game.Runes(P1, "order-rune", 8);
        game.Put("unit-2", Place.Base(P2));
        var big = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "harnessed-dragon")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new ChooseTargets { Targets = [big] });
        PassBoth(engine);

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        Assert.Contains(game.State.At(Place.Base(P2)), id => game.State[id].CardId == "unit-2");
    }
}
