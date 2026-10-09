using CromoBound.Engine.Effects.Steps;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What this engine can run (spec §7). A card whose file uses anything else plays by hand, like an Unmapped card.</summary>
internal static class EffectsSupport
{
    /// <summary>Keywords the 2a rules core already enforces. The others arrive with Plans E and F.</summary>
    private static readonly HashSet<MechanicalKeyword> Keywords =
    [
        MechanicalKeyword.Accelerate, MechanicalKeyword.Action, MechanicalKeyword.Reaction, MechanicalKeyword.Hidden,
        MechanicalKeyword.Ganking, MechanicalKeyword.Tank, MechanicalKeyword.Backline, MechanicalKeyword.Temporary,
        MechanicalKeyword.Unique,
    ];

    /// <summary>One line per construct the engine can't run; empty when it runs the whole file.</summary>
    public static IReadOnlyList<string> Problems(EffectsFile file)
    {
        var problems = new List<string>();
        if (file.Overrides is not null) problems.Add("overrides");
        if (file.AdditionalCosts.Count > 0) problems.Add("additionalCosts");
        if (file.AsYouPlay.Count > 0) problems.Add("asYouPlay");
        foreach (var entry in file.Keywords)
            if (!Keywords.Contains(entry.Keyword)) problems.Add($"keyword {entry.Keyword}");
        for (var i = 0; i < file.Abilities.Count; i++)
        {
            var at = $"abilities[{i}]";
            if (file.Abilities[i] is not SpellAbility spell)
            {
                problems.Add($"{at}: {file.Abilities[i].GetType().Name.Replace("Ability", "")} ability");
                continue;
            }
            if (spell.Condition is not null || spell.ActiveIn is not null || spell.Script is not null)
                problems.Add($"{at}: condition, activeIn or script");
            for (var j = 0; j < spell.Steps.Count; j++) CheckStep(spell.Steps[j], $"{at}.steps[{j}]", problems);
        }
        return problems;
    }

    private static void CheckStep(Step step, string at, List<string> problems)
    {
        if (!StepRegistry.Supports(step.GetType()))
        {
            problems.Add($"{at}: step {step.GetType().Name.Replace("Step", "")}");
            return;
        }
        if (step.Script is not null || step.Chooser is not null) problems.Add($"{at}: script or chooser");
        if (step.Player is { Kind: null }) problems.Add($"{at}: player");
        switch (step)
        {
            case DrawStep draw:
                CheckValue(draw.Amount, at, problems);
                break;
            case BurnStep burn:
                CheckValue(burn.Amount, at, problems);
                break;
            case DealStep deal:
                CheckValue(deal.Amount, at, problems);
                if (deal.Split is not null || deal.Bonus is not null || deal.Source is not null) problems.Add($"{at}: split, bonus or source");
                break;
            case ChannelStep channel:
                CheckValue(channel.Count, at, problems);
                break;
            case GainXpStep xp:
                CheckValue(xp.Amount, at, problems);
                break;
        }
        if (step is TargetStep target && !IsSupportedTarget(target.Target)) problems.Add($"{at}: target");
    }

    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (value.Literal is null) problems.Add($"{at}: value");
    }

    private static bool IsSupportedTarget(ObjectRef reference) =>
        reference.Ref == RefKind.Self || (TargetSlots.IsTarget(reference) && IsSupportedFilter(reference.Filter));

    /// <summary>Filters may use relation (Friendly or Enemy), type, token and other; nothing else yet.</summary>
    private static bool IsSupportedFilter(Filter? filter) => filter is null
        || ((filter.Relation is null or Relation.Friendly or Relation.Enemy)
            && filter.Controller is null && filter.Owner is null && filter.Location is null && filter.Zone is null
            && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
            && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Mighty is null
            && filter.Keyword is null && filter.Not is null);
}
