using CromoBound.Engine.Effects.Steps;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What this engine can run (spec §7). A card whose file uses anything else plays by hand, like an Unmapped card.</summary>
internal static class EffectsSupport
{
    /// <summary>Keywords the engine runs. The others arrive later in Plan F.</summary>
    private static readonly HashSet<MechanicalKeyword> Keywords =
    [
        MechanicalKeyword.Accelerate, MechanicalKeyword.Action, MechanicalKeyword.Reaction, MechanicalKeyword.Hidden,
        MechanicalKeyword.Ganking, MechanicalKeyword.Tank, MechanicalKeyword.Backline, MechanicalKeyword.Temporary,
        MechanicalKeyword.Unique, MechanicalKeyword.Deathknell, MechanicalKeyword.Vision, MechanicalKeyword.Hunt, MechanicalKeyword.Empower,
        MechanicalKeyword.Assault, MechanicalKeyword.Shield, MechanicalKeyword.Deflect,
        MechanicalKeyword.Ambush, MechanicalKeyword.Equip, MechanicalKeyword.Weaponmaster,
    ];

    /// <summary>The keywords that take a value (Hunt 3, Assault 2), a cost (Empower, Equip) or steps (Deathknell). Any other
    /// parameter would be ignored by the engine, so it is reported (spec §7: never silent).</summary>
    private static readonly HashSet<MechanicalKeyword> WithValue =
        [MechanicalKeyword.Hunt, MechanicalKeyword.Assault, MechanicalKeyword.Shield, MechanicalKeyword.Deflect];
    private static readonly HashSet<MechanicalKeyword> WithCost = [MechanicalKeyword.Empower, MechanicalKeyword.Equip];
    private static readonly HashSet<MechanicalKeyword> WithSteps = [MechanicalKeyword.Deathknell];

    /// <summary>One line per construct the engine can't run; empty when it runs the whole file. <paramref name="type"/> is the
    /// card's type: which trigger forms make sense depends on it.</summary>
    public static IReadOnlyList<string> Problems(EffectsFile file, CardType type)
    {
        var problems = new List<string>();
        if (file.Overrides is not null) problems.Add("overrides");
        for (var c = 0; c < file.AdditionalCosts.Count; c++)
            if (!IsSupportedAdditionalCost(file.AdditionalCosts[c])) problems.Add($"additionalCosts[{c}]");
        if (file.AsYouPlay.Count > 0) problems.Add("asYouPlay");
        for (var k = 0; k < file.Keywords.Count; k++) CheckKeyword(file.Keywords[k], $"keywords[{k}]", problems);
        for (var i = 0; i < file.Abilities.Count; i++) CheckAbility(file.Abilities[i], type, $"abilities[{i}]", problems);
        return problems;
    }

    /// <summary>A keyword's parameters must be ones it takes; Hunt needs its value, Empower and Equip a plain cost, and Deathknell's
    /// steps run like a trigger's.</summary>
    private static void CheckKeyword(KeywordEntry entry, string at, List<string> problems)
    {
        if (!Keywords.Contains(entry.Keyword))
        {
            problems.Add($"keyword {entry.Keyword}");
            return;
        }
        if (entry.Value is not null && !WithValue.Contains(entry.Keyword)) problems.Add($"{at}: value");
        if (entry.Cost is not null && !WithCost.Contains(entry.Keyword)) problems.Add($"{at}: cost");
        if (entry.Steps.Count > 0 && !WithSteps.Contains(entry.Keyword)) problems.Add($"{at}: steps");
        if (entry.Keyword == MechanicalKeyword.Deathknell) CheckSteps(entry.Steps, at, problems, targets: false);
        if (entry.Keyword == MechanicalKeyword.Hunt && entry.Value is null) problems.Add($"{at}: value");
        if (WithCost.Contains(entry.Keyword) && (entry.Cost is null || entry.Cost.ExhaustSelf is not null
            || (entry.Cost.Actions.Count > 0 && !(entry.Keyword == MechanicalKeyword.Equip
                && entry.Cost.Actions is [SpendXpStep { Amount.Literal: not null } spend] && IsPlain(spend)))))
            problems.Add($"{at}: cost");
    }

    private static void CheckAbility(Ability ability, CardType type, string at, List<string> problems)
    {
        if (ability is not (SpellAbility or TriggeredAbility or ActivatedAbility or PassiveAbility))
        {
            problems.Add($"{at}: {ability.GetType().Name.Replace("Ability", "")} ability");
            return;
        }
        var condition = ability.Condition is null || (ability is PassiveAbility && IsSupportedCondition(ability.Condition));
        if (!condition || ability.ActiveIn is not null || ability.Script is not null)
            problems.Add($"{at}: condition, activeIn or script");
        switch (ability)
        {
            case SpellAbility spell:
                CheckSteps(spell.Steps, at, problems, targets: true);
                break;
            case TriggeredAbility triggered:
                if (!IsSupportedTrigger(triggered.Trigger, type)) problems.Add($"{at}: trigger");
                if (triggered.If is not null || triggered.Optional is not null || triggered.Cost is not null || triggered.Limit is not null)
                    problems.Add($"{at}: if, optional, cost or limit");
                CheckSteps(triggered.Steps, at, problems, targets: true);
                break;
            case ActivatedAbility activated:
                if (activated.UseOnlyIf is not null || activated.Limit is not null) problems.Add($"{at}: useOnlyIf or limit");
                if (activated.Cost is { } cost && !IsSupportedCost(cost)) problems.Add($"{at}: cost");
                CheckSteps(activated.Steps, at, problems, targets: true);
                break;
            case PassiveAbility passive:
                if (passive.While is not null && !IsSupportedCondition(passive.While)) problems.Add($"{at}: while");
                foreach (var modifier in passive.Modifiers)
                    if (!IsSupportedModifier(modifier)) problems.Add($"{at}: modifier {modifier.GetType().Name.Replace("Modifier", "")}");
                break;
        }
    }

    /// <summary>A modifier of a kind <see cref="Modifiers"/> evaluates: on the card itself, a literal energy cost reduction, the
    /// permission to be played to a battlefield with enemy units, or a granted Assault, Deflect, Ganking, Shield or Tank; on the
    /// card itself or its host, a might change.</summary>
    private static bool IsSupportedModifier(Modifier modifier) => modifier switch
    {
        CostReductionModifier reduction => modifier.AppliesTo?.Ref == RefKind.Self && reduction.Energy?.Literal is not null
            && reduction.Power.Count == 0 && reduction.Minimum is null && reduction.FromZone is null,
        PermissionModifier permission => modifier.AppliesTo?.Ref == RefKind.Self
            && permission.Permission == Permission.PlayToBattlefieldWithEnemyUnits,
        ModifyMightModifier might => modifier.AppliesTo?.Ref is RefKind.Self or RefKind.Host && IsSupportedValue(might.Amount),
        GrantKeywordModifier grant => modifier.AppliesTo?.Ref == RefKind.Self
            && grant.Keyword.Keyword is MechanicalKeyword.Assault or MechanicalKeyword.Deflect or MechanicalKeyword.Ganking
                or MechanicalKeyword.Shield or MechanicalKeyword.Tank
            && grant.Keyword.Cost is null && grant.Keyword.Steps.Count == 0,
        _ => false,
    };

    /// <summary>The forms the <see cref="TriggerWatcher"/> maps: the source's own Dies, BecameEmpowered, Played, Hold or Conquer
    /// (a card that can be their subject, so not a battlefield), and a battlefield's "when you hold (or conquer) here".</summary>
    private static bool IsSupportedTrigger(Trigger trigger, CardType type)
    {
        if (trigger.Filter is not null || trigger.Phase is not null) return false;
        var own = type != CardType.Battlefield && trigger.Subject?.Ref == RefKind.Self && trigger.By is null && trigger.Where is null;
        var here = type == CardType.Battlefield && trigger.Subject is null && trigger.By?.Kind == PlayerKind.You
            && trigger.Where?.Ref == RefKind.Here;
        return trigger.Event switch
        {
            TriggerEvent.Dies or TriggerEvent.BecameEmpowered or TriggerEvent.Played => own,
            TriggerEvent.Hold or TriggerEvent.Conquer => own || here,
            _ => false,
        };
    }

    /// <summary>Energy, power, exhausting the source, and at most one cost action: recycling a number of cards from your trash,
    /// killing the source, or spending a number of XP.</summary>
    private static bool IsSupportedCost(Cost cost) => cost.Actions switch
    {
        [] => true,
        [RecycleStep recycle] => recycle.Target is null && recycle.Count?.Literal is not null
            && recycle.From is { Zone: Zone.Trash, Position: null } from && (from.Owner is null || from.Owner.Kind == PlayerKind.You)
            && IsPlain(recycle),
        [KillStep kill] => kill.Target.Ref == RefKind.Self && IsPlain(kill),
        [SpendXpStep spend] => spend.Amount.Literal is not null && IsPlain(spend),
        _ => false,
    };

    /// <summary>Energy and power, or one kill of a unit selector with a supported filter; no steps on paying, no cost changes.</summary>
    private static bool IsSupportedAdditionalCost(AdditionalCost cost) =>
        cost.OnPaid.Count == 0 && cost.ModifiesCost is null && cost.Cost.ExhaustSelf is null
        && cost.Cost.Actions switch
        {
            [] => true,
            [KillStep kill] => kill.Target is { Select: SelectKind.Unit, Count: not null } target && IsSupportedFilter(target.Filter) && IsPlain(kill),
            _ => false,
        };

    /// <summary>A cost action acts for the ability's controller and names nothing else.</summary>
    private static bool IsPlain(Step step) => step.Player is null && step.Chooser is null && step.Script is null && step.Store is null;

    private static void CheckSteps(IReadOnlyList<Step> steps, string at, List<string> problems, bool targets)
    {
        for (var j = 0; j < steps.Count; j++) CheckStep(steps[j], $"{at}.steps[{j}]", problems, targets);
    }

    /// <summary><paramref name="targets"/>: the top-level steps of a spell, trigger or activation, whose target selectors are slots chosen
    /// when the ability goes on the chain; elsewhere a selector is chosen on resolution.</summary>
    private static void CheckStep(Step step, string at, List<string> problems, bool targets)
    {
        if (!StepRegistry.Supports(step.GetType()))
        {
            problems.Add($"{at}: step {step.GetType().Name.Replace("Step", "")}");
            return;
        }
        if (step.Script is not null || step.Chooser is not null) problems.Add($"{at}: script or chooser");
        if (step.Player is not null && !UsesPlayer(step)) problems.Add($"{at}: player");
        else if (step.Player is { Kind: null, Var: null }) problems.Add($"{at}: player");
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
                if (deal.Split is not null || deal.Bonus is not null) problems.Add($"{at}: split or bonus");
                if (deal.Source is { } source && source.Ref != RefKind.Self && source.Var is null) problems.Add($"{at}: source");
                break;
            case ChannelStep channel:
                CheckValue(channel.Count, at, problems);
                break;
            case GainXpStep xp:
                CheckValue(xp.Amount, at, problems);
                break;
            case ModifyMightStep might:
                CheckValue(might.Amount, at, problems);
                if (might.Duration is not (null or Duration.ThisTurn)) problems.Add($"{at}: duration");
                break;
            case ChoosePlayerStep choose:
                if (choose.Filter is not null && !IsPlayerFilter(choose.Filter)) problems.Add($"{at}: filter");
                break;
            case ChooseCardStep card:
                if (card.From is not { Zone: Zone.Trash, Position: null } || card.From.Owner is { Kind: null, Var: null })
                    problems.Add($"{at}: from");
                if (!IsSupportedFilter(card.Filter) || card.Filter is { Mighty: not null } or { Location: not null } or { Not: not null })
                    problems.Add($"{at}: filter");
                CheckValue(card.Count, at, problems);
                break;
            case OptionalStep optional:
                if (optional.Reflexive != true || optional.Cost is not null) problems.Add($"{at}: optional");
                CheckSteps(optional.Steps, at, problems, targets: false);
                break;
            case PredictStep predict:
                if (predict.Amount.Literal != 1) problems.Add($"{at}: value");
                break;
            case AttachStep:
                problems.Add($"{at}: attach");
                break;
            case PlayStep play:
                if (play.Card.Var is null || play.From is not null || play.For is not null || play.Location is not null
                    || play.Exhausted is not null || play.Cost is not (null or PlayCostMode.IgnoreAll))
                    problems.Add($"{at}: play");
                break;
        }
        if (step is TargetStep target && !IsSupportedTarget(target, targets)) problems.Add($"{at}: target");
    }

    /// <summary>The steps that act for a player; on any other step a "player" would be ignored.</summary>
    private static bool UsesPlayer(Step step) => step is DrawStep or BurnStep or ChannelStep or GainXpStep or PredictStep;

    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (!IsSupportedValue(value)) problems.Add($"{at}: value");
    }

    /// <summary>A literal, a variable, a property of Self, Host or a variable, or a sum or product of supported values.</summary>
    public static bool IsSupportedValue(Value value) =>
        value.Literal is not null
        || value.Var is not null
        || (value.Prop is ValueProperty.Might or ValueProperty.EnergyCost or ValueProperty.Damage or ValueProperty.EmpowerCount
            && value.Of is { } of && (of.Ref is RefKind.Self or RefKind.Host || of.Var is not null))
        || (value.Sum is { Count: > 0 } sum && sum.All(IsSupportedValue))
        || (value.Mul is { Count: > 0 } mul && mul.All(IsSupportedValue));

    /// <summary>all, any, not, empowered, legion, exists (Self, a variable, or a selector with a supported filter), compare of supported
    /// values, paid and turnOf (You or Opponent).</summary>
    public static bool IsSupportedCondition(Condition condition) =>
        (condition.All is { } all && all.All(IsSupportedCondition))
        || (condition.Any is { } any && any.All(IsSupportedCondition))
        || (condition.Not is { } not && IsSupportedCondition(not))
        || condition.Empowered is not null
        || condition.Legion is not null
        || (condition.Exists is { } exists && (exists.Ref == RefKind.Self || exists.Var is not null
            || (exists.Select is SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent && IsSupportedFilter(exists.Filter))))
        || (condition.Compare is { } compare && IsSupportedValue(compare.Left) && IsSupportedValue(compare.Right))
        || condition.Paid is not null
        || condition.TurnOf is { Kind: PlayerKind.You or PlayerKind.Opponent };

    /// <summary>Self, Host, a variable, or a unit, gear or permanent selector with a supported filter that is an "all" selector, a
    /// slot (a count or upTo selector in the top-level steps of a spell, trigger or activation, <paramref name="targets"/>) or, in
    /// any other step, a count or upTo selector on a step type that chooses on resolution (<see cref="ChoosesOnResolution"/>).</summary>
    private static bool IsSupportedTarget(TargetStep step, bool targets)
    {
        var reference = step.Target;
        if (reference.Ref is RefKind.Self or RefKind.Host || reference.Var is not null) return true;
        if (reference.Select is not (SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent) || !IsSupportedFilter(reference.Filter))
            return false;
        return reference.All == true || (TargetSlots.IsTarget(reference) && (targets || ChoosesOnResolution(step)));
    }

    /// <summary>The steps whose handler asks for a non-slot selector through <see cref="ResolutionChoice"/>.</summary>
    private static bool ChoosesOnResolution(Step step) => step is DealStep or KillStep or ModifyMightStep;

    /// <summary>Card filters may use relation (Friendly or Enemy), type, token, other, mighty, location (Here, or any battlefield)
    /// and a supported not; nothing else yet.</summary>
    public static bool IsSupportedFilter(Filter? filter) =>
        filter is null
        || (filter.Relation is null or Relation.Friendly or Relation.Enemy && HasOnlyBasicFields(filter)
            && (filter.Location is null || filter.Location is { Ref: RefKind.Here } || filter.Location is { Select: SelectKind.Battlefield, Filter: null })
            && (filter.Not is null || IsSupportedFilter(filter.Not)));

    /// <summary>A ChoosePlayer filter: a relation of Opponent or Self and nothing else.</summary>
    private static bool IsPlayerFilter(Filter filter) =>
        filter.Relation is Relation.Opponent or Relation.Self && filter.Type is null && filter.Token is null && filter.Other is null
        && filter.Mighty is null && filter.Location is null && filter.Not is null && HasOnlyBasicFields(filter);

    /// <summary>None of the fields beyond relation, type, token, other, mighty, location and not is set.</summary>
    private static bool HasOnlyBasicFields(Filter filter) =>
        filter.Controller is null && filter.Owner is null && filter.Zone is null
        && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
        && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Keyword is null;
}
