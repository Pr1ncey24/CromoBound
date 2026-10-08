using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Tests;

internal static class MatchTestExtensions
{
    public static SubmitResult Accept(this Match match, PlayerId player, PlayerAction action)
    {
        var result = match.Submit(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    public static T Decision<T>(this Match match) where T : PendingDecision => Assert.IsType<T>(match.Pending);

    /// <summary>A text fingerprint of a match: stage, score, every event, every object and the turn state. Equal snapshots = same match.</summary>
    public static string Snapshot(this Match match)
    {
        var parts = new List<string> { $"{match.Stage} game {match.GameNumber} wins {string.Join(",", match.Result.GameWins)}" };
        parts.AddRange(match.Events.Select(e => $"{e.Sequence}: {e}"));
        if (match.Game is { } game)
        {
            parts.AddRange(game.State.Objects.Select(o => $"{o.Id} {o.CardId} @{o.Place} ctrl {o.Controller} ex {o.Exhausted} dmg {o.Damage}"));
            parts.AddRange(game.State.Players.Select(p => $"{p.Id} pts {p.Points} energy {p.Pool.Energy}"));
            var turn = game.State.Turn;
            parts.Add($"turn {turn.Number} {turn.Phase} priority {turn.Priority} chain {game.State.Chain.Count} pending {game.Pending?.GetType().Name}");
        }
        return string.Join("\n", parts);
    }

    /// <summary>The roll-off winner plays first, then (if asked) both players keep their decks: up to the first mulligan.</summary>
    public static Match ToMulligan(this Match match)
    {
        if (match.Pending is ChoosePlayOrderDecision order) match.Accept(order.Player, new ChoosePlayOrder(true));
        while (match.Pending is SideboardDecision sideboard) match.Accept(sideboard.Players[0], new SubmitSideboard());
        return match;
    }

    /// <summary>Bo3: every waiting player picks their first offered battlefield.</summary>
    public static Match PickFirstBattlefields(this Match match)
    {
        while (match.Pending is PickBattlefieldDecision pick)
        {
            var player = pick.Players[0];
            match.Accept(player, new PickBattlefield(pick.Choices.Single(c => c.Player == player).Printings[0]));
        }
        return match;
    }

    /// <summary>Up to the first decision of the first turn: nobody mulligans.</summary>
    public static Match ToPlay(this Match match)
    {
        match.ToMulligan();
        while (match.Pending is MulliganDecision mulligan) match.Accept(mulligan.Player, new Mulligan());
        return match;
    }
}
