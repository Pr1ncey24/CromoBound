using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class DrawHandler : StepHandler<DrawStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DrawStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Draw(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}

internal sealed class BurnHandler : StepHandler<BurnStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, BurnStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Burn(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}
