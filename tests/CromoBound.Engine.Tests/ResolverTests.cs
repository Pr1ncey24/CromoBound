using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ResolverTests
{
    private static EffectContext Context(PlayerId controller, ObjectId? source = null, params ObjectRef[] slots) =>
        new() { Controller = controller, Source = source, SourceCardId = "spell", Slots = slots };

    private static ObjectRef Select(SelectKind kind, Relation? relation = null, bool other = false) => new()
    {
        Select = kind,
        Count = 1,
        Filter = relation is null && !other ? null : new Filter { Relation = relation, Other = other ? true : null },
    };

    [Fact]
    public void Unit_candidates_are_board_units_in_id_order_filtered_by_relation()
    {
        var game = new TestGame();
        var mine = game.Put("unit-2", Place.Base(P1));
        var theirs = game.Put("unit-3", Place.Base(P2));
        game.Put("unit-2", Place.Trash(P1));
        game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();
        var context = Context(P1);

        Assert.Equal(new[] { mine, theirs }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit)));
        Assert.Equal(new[] { theirs }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit, Relation.Enemy)));
        Assert.Equal(new[] { mine }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit, Relation.Friendly)));
    }

    [Fact]
    public void Permanents_include_gear_and_other_leaves_out_the_source()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var gear = game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(new[] { unit, gear }, ObjectResolver.Candidates(engine, Context(P1), Select(SelectKind.Permanent)));
        Assert.Equal(new[] { gear }, ObjectResolver.Candidates(engine, Context(P1, unit), Select(SelectKind.Permanent, other: true)));
    }

    [Fact]
    public void A_chosen_target_that_left_the_board_is_dropped()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var kept = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        var slot = new ObjectRef { Select = SelectKind.Unit, UpTo = 2 };
        var context = Context(P1, null, slot);
        context.Targets.Add([unit, kept]);

        engine.MoveCard(unit, Place.Trash(P1));

        Assert.Equal(new[] { kept }, ObjectResolver.Resolve(engine, context, slot));
    }

    [Fact]
    public void Self_means_the_source_while_it_exists()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(new[] { unit }, ObjectResolver.Resolve(engine, Context(P1, unit), ObjectRef.Self));
        engine.MoveCard(unit, Place.Trash(P1));
        Assert.Empty(ObjectResolver.Resolve(engine, Context(P1, unit), ObjectRef.Self));
    }

    [Fact]
    public void Equal_looking_target_selectors_are_separate_slots()
    {
        var first = new ObjectRef { Select = SelectKind.Unit, Count = 1 };
        var second = new ObjectRef { Select = SelectKind.Unit, Count = 1 };
        Step[] steps =
        [
            new DealStep { Amount = 3, Target = first },
            new DrawStep { Amount = 1 },
            new KillStep { Target = ObjectRef.Self },
            new KillStep { Target = new ObjectRef { Select = SelectKind.Unit, All = true } },
            new DealStep { Amount = 3, Target = second },
        ];

        var slots = TargetSlots.Of(steps);

        Assert.Equal(2, slots.Count);
        Assert.Equal(0, TargetSlots.IndexOf(slots, first));
        Assert.Equal(1, TargetSlots.IndexOf(slots, second));
        Assert.Equal(-1, TargetSlots.IndexOf(slots, ObjectRef.Self));
    }

    [Fact]
    public void Players_resolve_in_turn_order()
    {
        var game = new TestGame();
        var engine = game.Start(first: P2);
        var context = Context(P1);

        Assert.Equal(new[] { P1 }, PlayerResolver.Resolve(engine, context, null));
        Assert.Equal(new[] { P1 }, PlayerResolver.Resolve(engine, context, PlayerRef.You));
        Assert.Equal(new[] { P2 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.Opponent }));
        Assert.Equal(new[] { P2, P1 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.EachPlayer }));
        Assert.Equal(new[] { P2 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.EachOpponent }));
    }

    [Fact]
    public void Literal_values_resolve_to_themselves()
    {
        var engine = new TestGame().Start();

        Assert.Equal(4, ValueResolver.Resolve(engine, Context(P1), 4));
    }

    [Fact]
    public void Values_read_properties_variables_sums_and_products()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P1));
        game.State[unit].Damage = 1;
        var engine = game.Start();
        var context = Context(P1);
        context.Vars["picked"] = new EffectVar([unit], [], 2, true);
        var picked = ObjectRef.Variable("picked");

        Assert.Equal(3, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Might, Of = picked }));
        Assert.Equal(3, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.EnergyCost, Of = picked }));
        Assert.Equal(1, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Damage, Of = picked }));
        Assert.Equal(2, ValueResolver.Resolve(engine, context, new Value { Var = "picked" }));
        Assert.Equal(0, ValueResolver.Resolve(engine, context, new Value { Var = "missing" }));
        Assert.Equal(0, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Might, Of = ObjectRef.Variable("missing") }));
        Assert.Equal(5, ValueResolver.Resolve(engine, context, new Value { Sum = [2, new Value { Var = "picked" }, 1] }));
        Assert.Equal(6, ValueResolver.Resolve(engine, context, new Value { Mul = [3, new Value { Var = "picked" }] }));
    }

    [Fact]
    public void Conditions_check_existence_comparisons_paid_costs_and_whose_turn_it_is()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();
        var context = Context(P1, unit);
        context.Vars["body"] = new EffectVar([], [], 1, true);
        context.Vars["skipped"] = new EffectVar([], [], 0, false);
        var self = new Value { Prop = ValueProperty.Might, Of = ObjectRef.Self };

        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Exists = Select(SelectKind.Unit, Relation.Friendly) }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Exists = Select(SelectKind.Unit, Relation.Enemy) }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Compare = new Comparison(self, CompareOp.Gte, 3) }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Compare = new Comparison(self, CompareOp.Gt, 3) }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Paid = "body" }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Paid = "skipped" }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Paid = "missing" }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { TurnOf = PlayerRef.You }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { TurnOf = new PlayerRef { Kind = PlayerKind.Opponent } }));
    }

    [Fact]
    public void Filters_check_mighty_location_and_not()
    {
        var game = new TestGame();
        var small = game.Put("unit-2", Place.Battlefield(0));
        var big = game.Put("unit-3", Place.Battlefield(0));
        var home = game.Put("unit-3", Place.Base(P1));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        var engine = game.Start();
        var here = Context(P1, game.State.Battlefields[0].Card);
        static ObjectRef Units(Filter filter) => new() { Select = SelectKind.Unit, All = true, Filter = filter };

        Assert.Equal(new[] { big }, ObjectResolver.Candidates(engine, here, Units(new Filter { Mighty = true })));
        Assert.Equal(new[] { small, big }, ObjectResolver.Candidates(engine, here, Units(new Filter { Location = new ObjectRef { Ref = RefKind.Here } })));
        Assert.Equal(new[] { small, big },
            ObjectResolver.Candidates(engine, here, Units(new Filter { Location = new ObjectRef { Select = SelectKind.Battlefield } })));
        Assert.Equal(new[] { small, home }, ObjectResolver.Candidates(engine, here, Units(new Filter { Not = new Filter { Mighty = true } })));
    }

    [Fact]
    public void Host_is_the_unit_the_source_is_attached_to()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.State[gear].AttachedTo = unit;
        var loose = game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();
        var host = new ObjectRef { Ref = RefKind.Host };

        Assert.Equal(new[] { unit }, ObjectResolver.Resolve(engine, Context(P1, gear), host));
        Assert.Empty(ObjectResolver.Resolve(engine, Context(P1, loose), host));
    }
}
