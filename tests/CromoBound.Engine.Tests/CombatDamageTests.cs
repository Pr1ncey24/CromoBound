using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Tests;

public class CombatDamageTests
{
    private static readonly ObjectId A = new(1), B = new(2), C = new(3);

    private static DamageTarget T(ObjectId unit, int lethal, DamageGroup group = DamageGroup.Normal) => new(unit, lethal, group);

    private static DamageAssignment D(ObjectId unit, int amount) => new(unit, amount);

    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(-2, 0, 1)]
    public void Lethal_is_at_least_one_and_counts_existing_damage(int might, int damage, int expected) =>
        Assert.Equal(expected, CombatDamage.Lethal(might, damage));

    [Fact]
    public void Lethal_in_order_with_one_partial_is_valid() =>
        Assert.Null(CombatDamage.Validate([T(A, 2), T(B, 3)], 4, [D(A, 2), D(B, 2)]));

    [Fact]
    public void Two_partially_damaged_units_are_invalid() =>
        Assert.NotNull(CombatDamage.Validate([T(A, 2), T(B, 3)], 3, [D(A, 1), D(B, 2)]));

    [Fact]
    public void Excess_damage_waits_until_every_unit_has_lethal()
    {
        Assert.NotNull(CombatDamage.Validate([T(A, 2), T(B, 3)], 4, [D(A, 4)]));
        Assert.Null(CombatDamage.Validate([T(A, 2), T(B, 3)], 7, [D(A, 4), D(B, 3)]));
    }

    [Fact]
    public void Tanks_first_and_backline_last()
    {
        DamageTarget[] targets = [T(A, 2, DamageGroup.Tank), T(B, 2), T(C, 2, DamageGroup.Backline)];

        Assert.NotNull(CombatDamage.Validate(targets, 2, [D(B, 2)]));
        Assert.NotNull(CombatDamage.Validate(targets, 4, [D(A, 2), D(C, 2)]));
        Assert.Null(CombatDamage.Validate(targets, 5, [D(A, 2), D(B, 2), D(C, 1)]));
    }

    [Fact]
    public void Total_must_match_and_targets_must_be_eligible()
    {
        Assert.NotNull(CombatDamage.Validate([T(A, 2)], 2, [D(A, 1)]));
        Assert.NotNull(CombatDamage.Validate([T(A, 2)], 2, [D(B, 2)]));
    }

    [Fact]
    public void Suggestion_fills_lethal_in_group_order_and_puts_overflow_on_the_last_unit() =>
        Assert.Equal(new[] { D(A, 1), D(B, 4) }, CombatDamage.Suggest([T(B, 2), T(A, 1, DamageGroup.Tank)], 5));
}
