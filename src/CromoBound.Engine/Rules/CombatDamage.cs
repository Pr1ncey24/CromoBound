using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;

namespace CromoBound.Engine.Rules;

/// <summary>Combat damage assignment rules (CR 465.2.c, spec §7.8).</summary>
public static class CombatDamage
{
    /// <summary>Damage still needed to kill: at least 1, enough to reach Might (negative Might counts as 0).</summary>
    public static int Lethal(int might, int damage) => Math.Max(1, Math.Max(might, 0) - damage);

    public static DamageGroup GroupOf(bool tank, bool backline) => tank ? DamageGroup.Tank : backline ? DamageGroup.Backline : DamageGroup.Normal;

    /// <summary>Null when the assignment is legal; otherwise the reason.</summary>
    public static string? Validate(IReadOnlyList<DamageTarget> targets, int total, IReadOnlyList<DamageAssignment> assignments)
    {
        var amounts = targets.ToDictionary(t => t.Unit, _ => 0);
        foreach (var assignment in assignments)
        {
            if (!amounts.ContainsKey(assignment.Unit)) return $"{assignment.Unit} is not a unit you can assign damage to.";
            if (assignment.Amount < 0) return "Damage amounts can't be negative.";
            amounts[assignment.Unit] += assignment.Amount;
        }

        var sum = amounts.Values.Sum();
        if (sum != total) return $"Assign exactly {total} damage (assigned {sum}).";
        if (targets.All(t => amounts[t.Unit] >= t.Lethal)) return null;
        if (targets.Any(t => amounts[t.Unit] > t.Lethal))
            return "No unit may take more than lethal damage until every unit has lethal damage.";
        if (targets.Count(t => amounts[t.Unit] > 0 && amounts[t.Unit] < t.Lethal) > 1)
            return "Each unit must take lethal damage before the next one takes any.";
        foreach (var group in Enum.GetValues<DamageGroup>())
        {
            var laterHit = targets.Any(t => t.Group > group && amounts[t.Unit] > 0);
            var groupUnfinished = targets.Any(t => t.Group == group && amounts[t.Unit] < t.Lethal);
            if (laterHit && groupUnfinished)
                return group == DamageGroup.Tank
                    ? "Tank units must take lethal damage first."
                    : "Backline units take damage only after every other unit has lethal damage.";
        }
        return null;
    }

    /// <summary>Lethal damage in group order (keeping the given order within a group); any excess goes to the last unit.</summary>
    public static IReadOnlyList<DamageAssignment> Suggest(IReadOnlyList<DamageTarget> targets, int total)
    {
        var result = new List<DamageAssignment>();
        var remaining = total;
        foreach (var target in targets.OrderBy(t => t.Group))
        {
            if (remaining == 0) break;
            var amount = Math.Min(remaining, target.Lethal);
            result.Add(new DamageAssignment(target.Unit, amount));
            remaining -= amount;
        }
        if (remaining > 0 && result.Count > 0)
            result[^1] = result[^1] with { Amount = result[^1].Amount + remaining };
        return result;
    }
}
