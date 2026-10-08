using CromoBound.Engine.Events;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum CleanupMode { Normal, Ending, Combat }

/// <summary>CR 318-324. Runs its steps until a pass changes nothing.</summary>
internal sealed class CleanupTask(CleanupMode mode) : GameTask
{
    /// <summary>Set once the repeated steps are stable, so a later decision (Task 5) doesn't rerun them.</summary>
    public bool StableReached { get; set; }

    public override bool Run(Game game) => game.RunCleanup(mode, this);
}

public sealed partial class Game
{
    internal bool RunCleanup(CleanupMode mode, CleanupTask task)
    {
        var special = mode;
        while (true)
        {
            var before = Changes;
            if (CheckWin()) return true;
            KillLethallyDamagedUnits();
            if (special == CleanupMode.Ending) EndingSteps();
            special = CleanupMode.Normal;
            if (Changes == before) break;
        }
        task.StableReached = true;
        CleanupDone();
        return true;
    }

    /// <summary>Step 1: at least the Victory Score and more points than every opponent (CR 194.2, 323.1).</summary>
    private bool CheckWin()
    {
        foreach (var player in State.Players)
        {
            if (!HasWon(player.Id)) continue;
            End(player.Id, GameEndReason.Points);
            return true;
        }
        return false;
    }

    /// <summary>Steps 3a-3b: units with non-zero damage ≥ Might are killed.</summary>
    private void KillLethallyDamagedUnits()
    {
        foreach (var unit in BoardUnits().Where(u => u.Damage > 0 && u.Damage >= Math.Max(MightOf(u.Id), 0)).ToList())
            Kill(unit.Id);
    }

    /// <summary>Ending special cleanup, steps 3c-3e: heal all units, "this turn" effects and Stunned expire, rune pools empty (CR 317.2).</summary>
    private void EndingSteps()
    {
        HealAllUnits();
        foreach (var instance in State.Objects.ToList())
        {
            if (instance.Modifiers.RemoveAll(m => m.Duration == Duration.ThisTurn) > 0) MarkDirty();
            if (instance.Stunned) SetStatus(instance.Id, StatusKind.Stunned, false);
        }
        foreach (var player in State.Players) player.Pool.Clear();
    }
}
