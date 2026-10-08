using CromoBound.Data;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Matches;

internal sealed partial class MatchCore
{
    /// <summary>Log index of the most recent action since play began in this game, by either player; -1 when there is none.</summary>
    public int UndoIndex() => Stage == MatchStage.Playing && PlayStartIndex >= 0 && Log.Count > PlayStartIndex ? Log.Count - 1 : -1;

    /// <summary>Either player may ask to roll back the game's last action, whoever made it; the opponent of the requester answers.</summary>
    public SubmitResult RequestUndo(PlayerId player)
    {
        if (Winner is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The match is over.");
        if (UndoRequestedBy is not null) return SubmitResult.Reject(RejectionCode.UndoNotAllowed, "An undo request is already waiting.");
        if (UndoIndex() < 0) return SubmitResult.Reject(RejectionCode.UndoNotAllowed, "There is no action to undo in this game.");
        UndoRequestedBy = player;
        return new SubmitResult(true, null, [new UndoRequested(player)]);
    }

    /// <summary>A fresh core with <paramref name="log"/> replayed. Same setup + same log = same match (spec §6.5).</summary>
    public static MatchCore Replay(MatchSetup setup, CardDatabase db, IEnumerable<LoggedAction> log)
    {
        var core = new MatchCore(setup, db);
        var index = 0;
        foreach (var entry in log)
        {
            var result = core.Submit(entry.Player, entry.Action);
            if (!result.Accepted)
                throw new InvalidDataException(
                    $"Log entry {index} ({entry.Action.GetType().Name} by {entry.Player}) was rejected on replay: {result.Rejection!.Message}");
            index++;
        }
        return core;
    }
}
