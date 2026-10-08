using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Awaken (ready everything the turn player controls), then queue Beginning, Scoring, Channel, Draw and Main (CR 315-316).</summary>
    internal void StartTurn(PlayerId player)
    {
        var turn = State.Turn;
        turn.Number++;
        turn.TurnPlayer = player;
        turn.Scored.Clear();
        turn.Priority = null;
        turn.Focus = null;
        Emit(new TurnStarted(player, turn.Number));
        EnterPhase(Phase.Awaken, TurnStep.None);
        foreach (var instance in State.Objects.Where(o => o.Controller == player && o.Place.IsBoard && o.Exhausted).ToList())
            SetStatus(instance.Id, StatusKind.Exhausted, false);
        Enqueue(new StepTask(g => g.BeginningStep()));
        Enqueue(new StepTask(g => g.ScoringStep()));
        Enqueue(new StepTask(g => g.ChannelPhase()));
        Enqueue(new StepTask(g => g.DrawPhase()));
        Enqueue(new StepTask(g => g.MainPhase()));
    }

    /// <summary>Temporary permanents the turn player controls are killed here, before scoring (CR 816.1.b).</summary>
    private void BeginningStep()
    {
        EnterPhase(Phase.Beginning, TurnStep.BeginningStep);
        var temporary = State.Objects
            .Where(o => o.Controller == State.Turn.TurnPlayer && o.Place.IsLocation && Has(o, DisplayKeyword.Temporary))
            .Select(o => o.Id)
            .ToList();
        foreach (var id in temporary) Kill(id);
    }

    private void ScoringStep()
    {
        EnterPhase(Phase.Beginning, TurnStep.ScoringStep);
        foreach (var battlefield in State.Battlefields.Where(b => b.Controller == State.Turn.TurnPlayer).ToList())
            Score(State.Turn.TurnPlayer, battlefield.Index, ScoreKind.Hold);
    }

    /// <summary>Channel 2 runes; the second player channels 3 on their first turn (turn 2, CR 485.7).</summary>
    private void ChannelPhase()
    {
        EnterPhase(Phase.Channel, TurnStep.None);
        Channel(State.Turn.TurnPlayer, State.Turn.Number == 2 ? 3 : 2);
    }

    private void DrawPhase()
    {
        EnterPhase(Phase.Draw, TurnStep.None);
        Draw(State.Turn.TurnPlayer, 1);
    }

    private void MainPhase()
    {
        EnterPhase(Phase.Main, TurnStep.None);
        foreach (var player in State.Players) player.Pool.Clear();
        State.Turn.Priority = State.Turn.TurnPlayer;
    }

    /// <summary>Ending step, Expiration step (Ending special cleanup), then the next player's turn (CR 317).</summary>
    private void EndTheTurn()
    {
        Enqueue(new StepTask(g => g.EnterPhase(Phase.Ending, TurnStep.EndingStep)));
        Enqueue(new StepTask(g =>
        {
            g.EnterPhase(Phase.Ending, TurnStep.ExpirationStep);
            g.Push(new CleanupTask(CleanupMode.Ending));
        }));
        Enqueue(new StepTask(g => g.StartTurn(g.State.Opponent(g.State.Turn.TurnPlayer))));
    }

    private void EnterPhase(Phase phase, TurnStep step)
    {
        State.Turn.Phase = phase;
        State.Turn.Step = step;
        Emit(new PhaseStarted(phase, step));
        MarkDirty();
    }
}
