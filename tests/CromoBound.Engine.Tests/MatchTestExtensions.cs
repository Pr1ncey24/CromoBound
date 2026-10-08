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
