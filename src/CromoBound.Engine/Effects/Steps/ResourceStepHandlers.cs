using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class ChannelHandler : StepHandler<ChannelStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChannelStep step)
    {
        var count = ValueResolver.Resolve(game, task.Context, step.Count);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (count <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        List<ObjectId> runes = [.. players.SelectMany(p => game.Channel(p, count, step.Exhausted == true))];
        if (runes.Count == 0) return StepOutcome.DidNothing;
        task.Result = new EffectVar(runes, players, runes.Count, true);
        return StepOutcome.Done;
    }
}

internal sealed class GainXpHandler : StepHandler<GainXpStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, GainXpStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.GainXp(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}
