using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Standard Move (CR 144): each unit is exhausted as the cost and moves; arriving where you don't have control applies Contested.</summary>
    private Rejection? MoveUnits(PlayerId player, StandardMove move, PriorityDecision options)
    {
        if (move.Units.Count == 0 || move.Units.Distinct().Count() != move.Units.Count)
            return Reject(RejectionCode.UnexpectedAction, "Choose one or more different units.");
        foreach (var unit in move.Units)
        {
            var option = options.Moves.FirstOrDefault(m => m.Unit == unit);
            if (option is null) return Reject(RejectionCode.UnknownObject, $"{unit} can't move now.");
            if (!option.Destinations.Contains(move.Destination))
                return Reject(RejectionCode.IllegalLocation, $"{unit} can't move to {move.Destination}.");
        }
        foreach (var unit in move.Units)
        {
            SetStatus(unit, StatusKind.Exhausted, true);
            MoveCard(unit, move.Destination);
        }
        if (move.Destination.Kind == PlaceKind.Battlefield) ApplyContested(player, move.Destination.Index!.Value);
        return null;
    }

    /// <summary>CR 190.3: a unit arriving at a battlefield its controller doesn't control applies Contested, if not already applied.</summary>
    internal void ApplyContested(PlayerId player, int battlefield)
    {
        var state = State.Battlefields[battlefield];
        if (state.Controller == player || state.ContestedBy is not null) return;
        state.ContestedBy = player;
        MarkDirty();
    }
}
