namespace CromoBound.Server.Matches;

/// <summary>The user in a seat, or making a challenge.</summary>
internal sealed record MatchSeat(int UserId, string UserName);
