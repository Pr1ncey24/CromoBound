using CromoBound.Data;
using CromoBound.Engine.Matches;

namespace CromoBound.Server.Hubs;

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
