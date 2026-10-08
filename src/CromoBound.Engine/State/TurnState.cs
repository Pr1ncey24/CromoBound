using CromoBound.Models.Effects;

namespace CromoBound.Engine.State;

public enum TurnStep { None, BeginningStep, ScoringStep, EndingStep, ExpirationStep }

public sealed class TurnState
{
    /// <summary>1-based turn counter; 0 before the first turn. Turn 2 is the second player's first turn.</summary>
    public int Number { get; set; }

    public PlayerId TurnPlayer { get; set; }
    public Phase Phase { get; set; }
    public TurnStep Step { get; set; }
    public PlayerId? Priority { get; set; }
    public PlayerId? Focus { get; set; }

    /// <summary>Battlefield indexes each player has scored this turn (CR 470: one score per battlefield per turn).</summary>
    public Dictionary<PlayerId, HashSet<int>> Scored { get; } = [];

    public bool HasScored(PlayerId player, int battlefield) => Scored.TryGetValue(player, out var set) && set.Contains(battlefield);

    public void MarkScored(PlayerId player, int battlefield)
    {
        if (!Scored.TryGetValue(player, out var set)) Scored[player] = set = [];
        set.Add(battlefield);
    }
}
