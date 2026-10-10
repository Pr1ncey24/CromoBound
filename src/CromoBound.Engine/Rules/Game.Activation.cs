using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum ActivationStage { Targets, Choose, Pay, Finalize, Cancelled }

/// <summary>Activating an ability (spec §5.3): choose the cost action's cards, pay energy and power, then pay the rest of the cost
/// and put the ability on the chain. Nothing is spent before the last stage, so cancelling leaves no trace.</summary>
internal sealed class ActivationTask(PlayerId player, ObjectId source, int ability) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Source { get; } = source;
    public int Ability { get; } = ability;
    public ActivationStage Stage { get; set; }
    public EffectContext? Context { get; set; }
    public IReadOnlyList<ObjectId> Chosen { get; set; } = [];
    public TotalCost? Cost { get; set; }

    public override bool Run(Game game) => game.RunActivation(this);
}

public sealed partial class Game
{
    /// <summary>The activated abilities of the player's permanents that can be started now. Runes have none (CardEffects leaves
    /// them to UseRune, CR 429).</summary>
    private IEnumerable<ActivateOption> ActivateOptions(PlayerId player)
    {
        foreach (var source in State.Objects.Where(o => o.Place.IsLocation && o.Controller == player))
        {
            var abilities = Effects.For(source.CardId).Abilities;
            for (var i = 0; i < abilities.Count; i++)
                if (abilities[i] is ActivatedAbility ability && CanActivate(player, source, ability)) yield return new ActivateOption(source.Id, i);
        }
    }

    /// <summary>Timing (Reaction: whenever you have priority; Action: while no chain exists; default: your own turn in Neutral Open),
    /// the ability's condition, a ready source when the cost exhausts it, and enough cards for its cost action (rules 416, 422).
    /// Energy and power are checked when paying, as for cards.</summary>
    private bool CanActivate(PlayerId player, CardInstance source, ActivatedAbility ability)
    {
        var timing = ability.Timing switch
        {
            Timing.Reaction => true,
            Timing.Action => !IsClosed,
            _ => IsNeutralOpenMain(player),
        };
        if (!timing) return false;
        var context = ActivationContext(player, source);
        if (ability.UseOnlyIf is { } condition && !ConditionResolver.Holds(this, context, condition)) return false;
        if (ability.Cost?.ExhaustSelf == true && source.Exhausted) return false;
        if (!HasSlotCandidates(ActivationContext(player, source, ability))) return false;
        return CostAction(ability) is not { } recycle || CostCards(context, recycle).Count >= CostCount(context, recycle);
    }

    private static EffectContext ActivationContext(PlayerId player, CardInstance source, ActivatedAbility? ability = null) =>
        new() { Controller = player, Source = source.Id, SourceCardId = source.CardId, Slots = ability is null ? [] : TargetSlots.Of(ability.Steps) };

    /// <summary>The ability's cost action, run in cost mode: "recycle N from your trash" is the only one EffectsSupport lets through.</summary>
    private static RecycleStep? CostAction(ActivatedAbility ability) => ability.Cost?.Actions is [RecycleStep recycle] ? recycle : null;

    private List<ObjectId> CostCards(EffectContext context, RecycleStep recycle) =>
        [.. ZoneResolver.Resolve(this, context, recycle.From!).SelectMany(place => State.At(place))];

    private int CostCount(EffectContext context, RecycleStep recycle) => ValueResolver.Resolve(this, context, recycle.Count!);

    internal bool RunActivation(ActivationTask task)
    {
        while (true)
        {
            if (task.Stage == ActivationStage.Cancelled) return true;
            if (OnBoard(task.Source) is not { } source) return true;
            var ability = (ActivatedAbility)Effects.For(source.CardId).Abilities[task.Ability];
            var context = task.Context ??= ActivationContext(task.Player, source, ability);
            switch (task.Stage)
            {
                case ActivationStage.Targets:
                    if (AskSlotTargets(task.Player, source.Id, context, null, () => task.Stage = ActivationStage.Cancelled)) return false;
                    if (task.Stage == ActivationStage.Targets) task.Stage = ActivationStage.Choose;
                    break;
                case ActivationStage.Choose:
                    if (AskCostCards(task, ability, context)) return false;
                    break;
                case ActivationStage.Pay:
                    task.Cost ??= ability.Cost is { } cost ? new TotalCost(cost.Energy ?? 0, cost.Power) : new TotalCost(0, []);
                    if (task.Cost.Energy == 0 && task.Cost.Power.Count == 0)
                    {
                        task.Stage = ActivationStage.Finalize;
                        break;
                    }
                    AskPay(task.Player, task.Cost, CardOf(source).Domains,
                        onPaid: () => task.Stage = ActivationStage.Finalize,
                        onCancel: () => task.Stage = ActivationStage.Cancelled,
                        onAdjust: adjusted => task.Cost = adjusted);
                    return false;
                default:
                    FinishActivation(task, source, ability, context);
                    return true;
            }
        }
    }

    /// <summary>Cost mode: the cost action's cards are chosen before paying; with too few the activation ends, and exactly enough
    /// is a forced choice. Moves the task on to paying, or asks and returns true.</summary>
    private bool AskCostCards(ActivationTask task, ActivatedAbility ability, EffectContext context)
    {
        task.Stage = ActivationStage.Pay;
        if (CostAction(ability) is not { } recycle) return false;
        var options = CostCards(context, recycle);
        var count = CostCount(context, recycle);
        if (options.Count < count)
        {
            task.Stage = ActivationStage.Cancelled;
            return false;
        }
        if (options.Count == count)
        {
            task.Chosen = options;
            Emit(new ChoiceMade(task.Player, "Cards", options));
            return false;
        }
        task.Stage = ActivationStage.Choose;
        Ask(new ChooseCardsDecision(task.Player, context.SourceCardId, options, count, count), (_, action) =>
        {
            if (action is CancelPlay)
            {
                task.Stage = ActivationStage.Cancelled;
                return null;
            }
            if (action is not ChooseCards choose) return Reject(RejectionCode.UnexpectedAction, "Choose the cards to recycle, or cancel.");
            if (CheckPick(choose.Cards, options, count, count, "cards") is { } rejection) return rejection;
            task.Chosen = [.. choose.Cards];
            task.Stage = ActivationStage.Pay;
            return null;
        });
        return true;
    }

    /// <summary>The rest of the cost is paid (exhaust the source; recycle the chosen cards that are still there), then the ability
    /// goes on the chain and its controller gets priority.</summary>
    private void FinishActivation(ActivationTask task, CardInstance source, ActivatedAbility ability, EffectContext context)
    {
        if (ability.Cost?.ExhaustSelf == true) SetStatus(source.Id, StatusKind.Exhausted, true);
        foreach (var card in task.Chosen.Where(State.Exists).ToList()) Recycle(card);
        if (State.Chain.Count == 0) ChainStartedByTrigger = false;
        var item = AddAbilityItem(task.Player, source.Id, source.CardId, AbilityKind.Activated, AbilityText(source.CardId, ability), ability.Steps, context);
        for (var slot = 0; slot < context.Targets.Count; slot++) Emit(new TargetsChosen(item.Id, slot, context.Targets[slot]));
        Emit(new AbilityActivated(source.Id, task.Ability, task.Player));
    }
}
