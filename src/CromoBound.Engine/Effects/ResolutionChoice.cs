using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>The objects a step acts on. Slots, Self, Host, variables and "all" selectors resolve as they always did; a count or
/// upTo selector that isn't a slot (a step inside an optional block) is chosen now by the controller, forced when there are
/// exactly as many candidates as required.</summary>
internal static class ResolutionChoice
{
    /// <summary>The objects, or null when it asked (the step returns Asked and runs again with the answer).</summary>
    public static IReadOnlyList<ObjectId>? Targets(Game game, ResolveEffectTask task, ObjectRef reference)
    {
        var context = task.Context;
        if (reference.Select is null || reference.All == true || TargetSlots.IndexOf(context.Slots, reference) >= 0
            || (reference.Count is null && reference.UpTo is null))
            return ObjectResolver.Resolve(game, context, reference);
        if (task.Answer is IReadOnlyList<ObjectId> chosen) return [.. chosen.Where(game.State.Exists)];
        var options = ObjectResolver.Candidates(game, context, reference);
        var min = Math.Min(reference.Count ?? 0, options.Count);
        var max = Math.Min(reference.Count ?? reference.UpTo ?? 0, options.Count);
        if (max == 0) return [];
        if (options.Count == min)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", options));
            return options;
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, min, max), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the cards.");
            if (Game.CheckPick(choose.Cards, options, min, max, "cards") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return null;
    }
}
