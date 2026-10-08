using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine;

public enum RejectionCode
{
    NotYourDecision, UnexpectedAction, WrongTiming, UnknownObject, IllegalLocation, InsufficientPayment,
    InvalidAssignment, InvalidSideboard, UndoNotAllowed, MatchOver, InvalidTarget,
}

/// <summary>Why an action was refused. A rejected action changes nothing.</summary>
public sealed record Rejection(RejectionCode Code, string Message);

public sealed record SubmitResult(bool Accepted, Rejection? Rejection, IReadOnlyList<GameEvent> Events)
{
    public static SubmitResult Reject(RejectionCode code, string message) => new(false, new Rejection(code, message), []);
}

public enum GameEndReason { Points, BurnOut, Concede }

public sealed record GameOutcome(PlayerId? Winner, GameEndReason Reason);
