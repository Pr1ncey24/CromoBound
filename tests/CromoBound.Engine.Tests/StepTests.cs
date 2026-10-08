using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class StepTests
{
    private static ObjectRef AUnit() => new() { Select = SelectKind.Unit, Count = 1 };

    /// <summary>Runs the steps for P1, with the given targets for each target slot, and returns the context afterwards.</summary>
    private static EffectContext Run(Game engine, IReadOnlyList<Step> steps, params ObjectId[][] targets)
    {
        var context = new EffectContext { Controller = P1, SourceCardId = "spell", Slots = TargetSlots.Of(steps) };
        foreach (var slot in targets) context.Targets.Add(slot);
        engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        return context;
    }

    [Fact]
    public void Draw_draws_for_the_named_players()
    {
        var game = new TestGame();
        var engine = game.Start();

        Run(engine, [new DrawStep { Amount = 2 }]);
        Run(engine, [new DrawStep { Amount = 1, Player = new PlayerRef { Kind = PlayerKind.EachPlayer } }]);

        Assert.Equal(4, game.State.At(Place.Hand(P1)).Count);
        Assert.Single(game.State.At(Place.Hand(P2)));
    }

    [Fact]
    public void Burn_puts_the_top_cards_into_the_trash()
    {
        var game = new TestGame();
        var engine = game.Start();
        var top = game.State.At(Place.MainDeck(P1)).Take(3).Select(id => game.State[id].CardId).ToList();

        Run(engine, [new BurnStep { Amount = 3 }]);

        Assert.Equal(3, game.State.At(Place.Trash(P1)).Count);
        Assert.Equal(6, game.State.At(Place.MainDeck(P1)).Count);
        Assert.Equal(top, game.State.At(Place.Trash(P1)).Select(id => game.State[id].CardId).ToList());
    }

    [Fact]
    public void Burning_past_an_empty_deck_burns_out()
    {
        var game = new TestGame();
        var engine = game.Start(filler: 2);

        var events = engine.RunNow(new ResolveEffectTask(
            new EffectContext { Controller = P1, SourceCardId = "spell" }, [new BurnStep { Amount = 2 }], _ => { }));

        Assert.Equal(1, game.State.Player(P2).Points);
        Assert.Contains(events, e => e is BurnedOut { Player.Index: 0 });
        Assert.Single(game.State.At(Place.Trash(P1)));
        Assert.Empty(game.State.At(Place.MainDeck(P1)));
    }

    [Fact]
    public void Deal_damages_the_target_and_cleanup_kills_lethal_damage()
    {
        var game = new TestGame();
        var hurt = game.Put("unit-3", Place.Base(P2));
        var doomed = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }], [hurt]);
        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }], [doomed]);

        Assert.Equal(2, game.State[hurt].Damage);
        Assert.False(game.State.Exists(doomed));
    }

    [Fact]
    public void Deal_to_a_target_that_left_does_nothing_and_stores_that_it_didnt_happen()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.MoveCard(unit, Place.Hand(P2));

        var context = Run(engine, [new DealStep { Amount = 2, Target = AUnit(), Store = "hit" }], [unit]);

        Assert.False(context.Vars["hit"].Happened);
        Assert.All(game.State.At(Place.Hand(P2)), id => Assert.Equal(0, game.State[id].Damage));
    }

    [Fact]
    public void Kill_moves_the_target_to_its_owners_trash()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        var context = Run(engine, [new KillStep { Target = AUnit(), Store = "killed" }], [unit]);

        Assert.False(game.State.Exists(unit));
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        Assert.True(context.Vars["killed"].Happened);
        Assert.Equal(new[] { unit }, context.Vars["killed"].Objects);
    }

    [Fact]
    public void Killing_a_gear_moves_it_to_the_trash_without_a_unit_death()
    {
        var game = new TestGame();
        var gear = game.Put("gear-1", Place.Base(P2));
        var engine = game.Start();
        var kill = new KillStep { Target = new ObjectRef { Select = SelectKind.Gear, Count = 1 } };
        var context = new EffectContext { Controller = P1, SourceCardId = "spell", Slots = TargetSlots.Of([kill]) };
        context.Targets.Add([gear]);

        var events = engine.RunNow(new ResolveEffectTask(context, [kill], _ => { }));

        Assert.False(game.State.Exists(gear));
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "gear-1");
        Assert.DoesNotContain(events, e => e is UnitDied);
    }

    [Fact]
    public void Two_target_slots_may_hit_the_same_unit()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }, new DealStep { Amount = 2, Target = AUnit() }], [unit], [unit]);

        Assert.False(game.State.Exists(unit));
    }

    [Fact]
    public void The_task_reports_done_once_after_the_last_step_and_the_game_asks_again()
    {
        var game = new TestGame();
        var engine = game.Start();
        var done = 0;

        engine.RunNow(new ResolveEffectTask(new EffectContext { Controller = P1, SourceCardId = "spell" },
            [new DrawStep { Amount = 1 }, new DrawStep { Amount = 1 }], _ => done++));

        Assert.Equal(1, done);
        Assert.Equal(3, game.State.At(Place.Hand(P1)).Count);
        Assert.IsType<Decisions.PriorityDecision>(engine.Pending);
    }
}
