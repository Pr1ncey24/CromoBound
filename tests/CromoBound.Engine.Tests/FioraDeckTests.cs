using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
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

    [Fact]
    public void Dorans_blade_gives_two_might_only_while_attached()
    {
        var game = Real("dorans-blade", "body-rune");
        var blade = game.Put("dorans-blade", Place.Base(P1));
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "body-rune", 1);
        var engine = game.Start();
        Assert.Equal(2, engine.MightOf(unit));

        engine.Accept(P1, new ActivateAbility(blade, 1));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(unit, game.State[blade].AttachedTo);
        Assert.Equal(4, engine.MightOf(unit));
        game.State[blade].AttachedTo = null;
        Assert.Equal(2, engine.MightOf(unit));
    }

    [Fact]
    public void Shepherds_heirloom_gives_xp_when_played_and_spends_it_to_equip()
    {
        var game = Real("shepherds-heirloom");
        game.Put("shepherds-heirloom", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "shepherds-heirloom")));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);
        Assert.Equal(1, game.State.Player(P1).Xp);

        var heirloom = game.First(Place.Base(P1), "shepherds-heirloom");
        engine.Accept(P1, new ActivateAbility(heirloom, 2));
        Assert.Equal(0, game.State.Player(P1).Xp);
        PassBoth(engine);

        Assert.Equal(4, engine.MightOf(unit));
        Assert.DoesNotContain(engine.Decision<PriorityDecision>().Activations, a => a.Source == heirloom);
    }

    [Fact]
    public void Fiora_victorious_gets_her_keywords_only_while_mighty()
    {
        var game = Real("fiora-victorious");
        var fiora = game.Put("fiora-victorious", Place.Base(P1));
        var engine = game.Start();
        var instance = game.State[fiora];

        Assert.False(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.Equal(0, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));

        instance.Modifiers.Add(new MightModifier(1, Duration.ThisTurn));

        Assert.True(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.True(engine.Has(instance, DisplayKeyword.Shield));
        Assert.Equal(1, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));
        instance.Role = CombatRole.Defender;
        Assert.Equal(6, engine.MightOf(fiora));
    }

    [Fact]
    public void Fiora_victorious_counts_her_equipment_might_when_checking_her_own_passive()
    {
        var game = Real("fiora-victorious", "dorans-blade");
        var fiora = game.Put("fiora-victorious", Place.Base(P1));
        var blade = game.Put("dorans-blade", Place.Base(P1));
        var engine = game.Start();
        var instance = game.State[fiora];
        Assert.Equal(4, engine.MightOf(fiora));
        Assert.False(engine.Has(instance, DisplayKeyword.Ganking));

        game.State[blade].AttachedTo = fiora;

        Assert.Equal(6, engine.MightOf(fiora));
        Assert.True(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.True(engine.Has(instance, DisplayKeyword.Shield));
        Assert.Equal(1, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));
        instance.Role = CombatRole.Defender;
        Assert.Equal(7, engine.MightOf(fiora));
        instance.Role = null;

        game.State[blade].AttachedTo = null;

        Assert.Equal(4, engine.MightOf(fiora));
        Assert.False(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.Equal(0, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));
    }
}
