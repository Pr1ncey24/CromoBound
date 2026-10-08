using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Matches;

internal sealed partial class MatchCore
{
    private void AfterGameChange()
    {
        if (Game?.Outcome is { } outcome) RecordGame(outcome);
    }

    /// <summary>The player concedes the current game at any moment, even before it starts: the opponent wins it.</summary>
    private Rejection? ConcedeGame(PlayerId player)
    {
        UndoRequestedBy = null;
        _pending = null;
        _handler = null;
        var winner = Opponent(player);
        if (Game is { Outcome: null } game) game.End(winner, GameEndReason.Concede);
        RecordGame(new GameOutcome(winner, GameEndReason.Concede));
        return null;
    }

    /// <summary>Records the game. Bo3 removes the battlefields used in it, but only if the game was set up (a game ended during the picks, play order or sideboarding uses none). The match ends at 1 win (Bo1) or 2 (Bo3); otherwise the next game begins.</summary>
    internal void RecordGame(GameOutcome outcome)
    {
        Emit(new GameRecorded(GameNumber, outcome.Winner, outcome.Reason));
        if (outcome.Winner is { } winner)
        {
            Wins[winner.Index]++;
            LastLoser = Opponent(winner);
        }
        if (Setup.Format == MatchFormat.Bo3 && Game is not null)
            foreach (var player in Players) Available[player.Index].Remove(Picks[player.Index]!);

        var needed = Setup.Format == MatchFormat.Bo1 ? 1 : 2;
        foreach (var player in Players)
        {
            if (Wins[player.Index] < needed) continue;
            Winner = player;
            Stage = MatchStage.Over;
            Emit(new MatchEnded(player));
            return;
        }
        BeginGame();
    }
}
