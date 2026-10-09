namespace CromoBound.Server.Storage;

internal enum MatchStatus { Running, Finished, Abandoned }

/// <summary>A match (spec §6.6): its two players by seat and the engine's saved record as JSON, rewritten after every accepted
/// action. A finished or abandoned match keeps its record.</summary>
internal sealed class MatchEntity
{
    public Guid Id { get; set; }
    public int Seat0UserId { get; set; }
    public int Seat1UserId { get; set; }
    public required string RecordJson { get; set; }
    public MatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
