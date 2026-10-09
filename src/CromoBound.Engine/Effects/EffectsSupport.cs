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
        for (var i = 0; i < file.Abilities.Count; i++) CheckAbility(file.Abilities[i], $"abilities[{i}]", problems);
        return problems;
    }

    private static void CheckAbility(Ability ability, string at, List<string> problems)
    {
        if (ability is not SpellAbility spell)
        {
            problems.Add($"{at}: {ability.GetType().Name.Replace("Ability", "")} ability");
            return;
        }
        if (spell.Condition is not null || spell.ActiveIn is not null || spell.Script is not null)
            problems.Add($"{at}: condition, activeIn or script");
        CheckSteps(spell.Steps, at, problems, targets: true);
    }

    private static void CheckSteps(IReadOnlyList<Step> steps, string at, List<string> problems, bool targets)
    {
        for (var j = 0; j < steps.Count; j++) CheckStep(steps[j], $"{at}.steps[{j}]", problems, targets);
    }

    /// <summary><paramref name="targets"/>: target selectors are allowed (a spell's steps, chosen while playing); elsewhere only Self.</summary>
    private static void CheckStep(Step step, string at, List<string> problems, bool targets)
    {
        if (!StepRegistry.Supports(step.GetType()))
        {
            problems.Add($"{at}: step {step.GetType().Name.Replace("Step", "")}");
            return;
        }
        if (step.Script is not null || step.Chooser is not null) problems.Add($"{at}: script or chooser");
        if (step.Player is { Kind: null, Var: null }) problems.Add($"{at}: player");
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
            case ChoosePlayerStep choose:
                if (choose.Filter is not null && !IsPlayerFilter(choose.Filter)) problems.Add($"{at}: filter");
                break;
            case ChooseCardStep card:
                if (card.From is not { Zone: Zone.Trash, Position: null } || card.From.Owner is { Kind: null, Var: null })
                    problems.Add($"{at}: from");
                if (!IsSupportedFilter(card.Filter)) problems.Add($"{at}: filter");
                CheckValue(card.Count, at, problems);
                break;
            case OptionalStep optional:
                if (optional.Reflexive != true || optional.Cost is not null) problems.Add($"{at}: optional");
                CheckSteps(optional.Steps, at, problems, targets: false);
                break;
            case PredictStep predict:
                if (predict.Amount.Literal != 1) problems.Add($"{at}: value");
                break;
            case PlayStep play:
                if (play.Card.Var is null || play.From is not null || play.For is not null || play.Location is not null
                    || play.Exhausted is not null || play.Cost is not (null or PlayCostMode.IgnoreAll))
                    problems.Add($"{at}: play");
                break;
        }
        if (step is TargetStep target && !IsSupportedTarget(target.Target, targets)) problems.Add($"{at}: target");
    }

    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (value.Literal is null) problems.Add($"{at}: value");
    }

    private static bool IsSupportedTarget(ObjectRef reference, bool targets) =>
        reference.Ref == RefKind.Self || (targets && TargetSlots.IsTarget(reference) && IsSupportedFilter(reference.Filter));

    /// <summary>Card filters may use relation (Friendly or Enemy), type, token and other; nothing else yet.</summary>
    private static bool IsSupportedFilter(Filter? filter) =>
        filter is null || (filter.Relation is null or Relation.Friendly or Relation.Enemy && HasOnlyBasicFields(filter));

    /// <summary>A ChoosePlayer filter: a relation of Opponent or Self and nothing else.</summary>
    private static bool IsPlayerFilter(Filter filter) =>
        filter.Relation is Relation.Opponent or Relation.Self && filter.Type is null && filter.Token is null && filter.Other is null
        && HasOnlyBasicFields(filter);

    /// <summary>None of the fields beyond relation, type, token and other is set.</summary>
    private static bool HasOnlyBasicFields(Filter filter) =>
        filter.Controller is null && filter.Owner is null && filter.Location is null && filter.Zone is null
        && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
        && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Mighty is null
        && filter.Keyword is null && filter.Not is null;
}
