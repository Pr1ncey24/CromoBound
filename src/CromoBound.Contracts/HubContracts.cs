using CromoBound.Data;
using CromoBound.Engine;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Contracts;

/// <summary>The answer to a challenge call: the challenge's or match's id, or why not (with the deck's problems for an illegal deck).</summary>
public sealed record HubReply(Guid? Id, string? Error, IReadOnlyList<DeckIssue>? DeckIssues = null)
{
    public static HubReply Done { get; } = new(null, null);

    public static HubReply Ok(Guid id) => new(id, null);

    public static HubReply Fail(string error) => new(null, error);
}

public enum ChallengeEnd
{
    Accepted,
    Declined,
    Cancelled,

    /// <summary>One of its players started another match.</summary>
    Withdrawn,
}

/// <summary>Someone challenged the receiving player.</summary>
public sealed record ChallengeNotice(Guid ChallengeId, string From, MatchFormat Format);

/// <summary>A challenge the receiving player made or received is closed.</summary>
public sealed record ChallengeClosedNotice(Guid ChallengeId, ChallengeEnd Reason);

/// <summary>A match started; the receiving player is in <see cref="Seat"/> and plays <see cref="Opponent"/>.</summary>
public sealed record MatchStartedNotice(Guid MatchId, string Opponent, PlayerId Seat);

/// <summary>The receiving player's own view of the match, sent after every accepted action.</summary>
public sealed record MatchViewNotice(Guid MatchId, PlayerView View);

public enum MatchEndReason
{
    Finished,

    /// <summary>The server was updated (or the saved match couldn't be replayed), so the match can't go on.</summary>
    Abandoned,
}

/// <summary>A match is over: the game wins by seat, and the winner's name (none when abandoned).</summary>
public sealed record MatchEndedNotice(Guid MatchId, MatchEndReason Reason, IReadOnlyList<int> GameWins, string? Winner);

/// <summary>GetMatch's answer: the player's running match and their view of it; or, once, the notice of a match of theirs that was
/// abandoned when the server restarted; or nothing.</summary>
public sealed record MatchReply(Guid? MatchId, PlayerView? View, MatchEndedNotice? Ended)
{
    public static MatchReply None { get; } = new(null, null, null);
}

/// <summary>Submit's answer (spec §6.3): whether the engine accepted the action, with the engine's own rejection when it didn't, or the
/// server's reason (no such match, an unreadable action, a save that failed).</summary>
public sealed record SubmitReply(bool Accepted, Rejection? Rejection, string? Error);
