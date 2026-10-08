using CromoBound.Engine.Actions;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Applies an answer to a priority decision. Grows as later tasks add actions.</summary>
    private Rejection? HandlePriority(PlayerId player, PlayerAction action)
    {
        var options = PriorityOptions(player);
        switch (action)
        {
            case UseRune use:
                return UseRuneNow(player, use);
            case EndTurn when options.CanEndTurn:
                EndTheTurn();
                return null;
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not possible now.");
        }
    }
}
