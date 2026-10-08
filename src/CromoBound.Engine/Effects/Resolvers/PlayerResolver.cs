using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns player references into players, in turn order (turn player first). A missing reference means "You".</summary>
internal static class PlayerResolver
{
    public static List<PlayerId> Resolve(Game game, EffectContext context, PlayerRef? reference)
    {
        var you = context.Controller;
        var target = reference ?? PlayerRef.You;
        return target.Kind switch
        {
            PlayerKind.You => [you],
            PlayerKind.Opponent => [game.State.Opponent(you)],
            PlayerKind.EachPlayer => [.. InTurnOrder(game)],
            PlayerKind.EachOpponent => [.. InTurnOrder(game).Where(p => p != you)],
            _ => target.Var is { } name && context.Vars.TryGetValue(name, out var stored) ? [.. stored.Players] : [],
        };
    }

    private static IEnumerable<PlayerId> InTurnOrder(Game game)
    {
        var turnPlayer = game.State.Turn.TurnPlayer;
        yield return turnPlayer;
        foreach (var player in game.State.Players)
            if (player.Id != turnPlayer) yield return player.Id;
    }
}
