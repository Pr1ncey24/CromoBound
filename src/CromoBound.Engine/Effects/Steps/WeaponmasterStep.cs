using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The step the Weaponmaster keyword stands for. It exists only in <see cref="CardEffects"/>, never in an effects file.</summary>
internal sealed record WeaponmasterStep : Step;

/// <summary>Weaponmaster (CR 821, spec §8.1): when the unit is played, its controller may attach an Equipment they control to it
/// by paying that Equipment's Equip cost minus [A] (one power symbol less). Only Equipment whose Equip cost the engine knows (a
/// mapped Equip keyword) is offered; other Equipment is attached by hand with ManualAttach.</summary>
internal sealed class WeaponmasterHandler : StepHandler<WeaponmasterStep>
{
    /// <summary>The chosen Equipment and the cost still to pay; Paid once the payment went through, Declined when it was cancelled.</summary>
    private sealed record Pick(ObjectId Gear, TotalCost Cost, bool Paid = false, bool Declined = false);

    protected override StepOutcome Run(Game game, ResolveEffectTask task, WeaponmasterStep step)
    {
        var context = task.Context;
        if (context.Source is not { } unit || !game.State.Exists(unit) || !game.State[unit].Place.IsLocation) return StepOutcome.DidNothing;
        switch (task.Progress)
        {
            case Pick { Declined: true }:
                return StepOutcome.DidNothing;
            case Pick { Paid: true } paid:
                return Attach(game, task, paid.Gear, unit);
            case Pick pick:
                game.AskPay(context.Controller, pick.Cost, game.CardOf(pick.Gear).Domains,
                    onPaid: () => task.Progress = pick with { Paid = true },
                    onCancel: () => task.Progress = pick with { Declined = true },
                    onAdjust: adjusted => task.Progress = pick with { Cost = adjusted });
                return StepOutcome.Asked;
        }
        var options = Equipment(game, context.Controller, unit);
        if (options.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> chosen)
        {
            if (chosen.Count == 0) return StepOutcome.DidNothing;
            var cost = EquipCost(game, chosen[0]);
            if (cost.Energy == 0 && cost.Power.Count == 0) return Attach(game, task, chosen[0], unit);
            task.Progress = new Pick(chosen[0], cost);
            return Run(game, task, step);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, 0, 1), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose an Equipment to attach, or none.");
            if (Game.CheckPick(choose.Cards, options, 0, 1, "Equipment") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    /// <summary>The player's gear in play, not already on this unit, whose Equip cost the engine knows.</summary>
    private static List<ObjectId> Equipment(Game game, PlayerId player, ObjectId unit) =>
    [
        .. game.State.Objects
            .Where(o => o.Place.IsLocation && o.Controller == player && o.AttachedTo != unit
                && game.CardOf(o).Type == CardType.Gear && EquipEntry(game, o) is not null)
            .Select(o => o.Id),
    ];

    private static KeywordEntry? EquipEntry(Game game, CardInstance gear) =>
        game.Effects.For(gear.CardId).KeywordEntries.FirstOrDefault(k => k.Keyword == MechanicalKeyword.Equip && k.Cost is not null);

    /// <summary>The Equip cost minus [A]: its first power symbol is dropped; energy is unchanged.</summary>
    private static TotalCost EquipCost(Game game, ObjectId gear)
    {
        var cost = EquipEntry(game, game.State[gear])!.Cost!;
        return new TotalCost(cost.Energy ?? 0, [.. cost.Power.Skip(1)]);
    }

    private static StepOutcome Attach(Game game, ResolveEffectTask task, ObjectId gear, ObjectId unit)
    {
        if (!game.State.Exists(gear) || !game.State[gear].Place.IsLocation) return StepOutcome.DidNothing;
        game.Attach(gear, unit);
        task.Result = new EffectVar([gear], [], null, true);
        return StepOutcome.Done;
    }
}
