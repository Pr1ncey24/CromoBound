using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum CleanupMode { Normal, Ending, Combat }

/// <summary>CR 318-324. Steps 1-8 repeat until a pass changes nothing; steps 9-10 may then start a staged showdown or combat.</summary>
internal sealed class CleanupTask(CleanupMode mode) : GameTask
{
    /// <summary>Set once steps 1-8 are stable, so answering the step 9-10 choice doesn't rerun them.</summary>
    public bool StableReached { get; set; }

    /// <summary>Set once the Ending or Combat special steps ran, so a rerun after a manual action does them only once.</summary>
    public bool SpecialDone { get; set; }

    public override bool Run(Game game) => game.RunCleanup(mode, this);
}

public sealed partial class Game
{
    internal bool RunCleanup(CleanupMode mode, CleanupTask task)
    {
        if (!task.StableReached)
        {
            var special = task.SpecialDone ? CleanupMode.Normal : mode;
            task.SpecialDone = true;
            while (true)
            {
                var before = Changes;
                if (CheckWin()) return true;
                AssignCombatRoles();
                KillLethallyDamagedUnits();
                if (special == CleanupMode.Ending) EndingSteps();
                if (special == CleanupMode.Combat) CombatSteps();
                special = CleanupMode.Normal;
                LoseControlOfEmptyBattlefields();
                RecallStrays();
                StageShowdownsAndCombats();
                ClearContested();
                if (Changes == before) break;
            }
            task.StableReached = true;
            CleanupDone();
        }
        return StartStaged();
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

    /// <summary>Step 2: units at the combat battlefield take their side's role; units elsewhere lose it.</summary>
    private void AssignCombatRoles()
    {
        var combat = State.Showdown is { IsCombat: true } running ? running : null;
        foreach (var unit in BoardUnits().ToList())
        {
            CombatRole? role = combat is not null && unit.Place == Place.Battlefield(combat.Battlefield)
                ? unit.Controller == combat.Attacker ? CombatRole.Attacker : CombatRole.Defender
                : null;
            if (unit.Role == role) continue;
            unit.Role = role;
            MarkDirty();
        }
    }

    /// <summary>Steps 3a-3b: units with non-zero damage ≥ Might are killed.</summary>
    private void KillLethallyDamagedUnits()
    {
        foreach (var unit in BoardUnits().Where(u => u.Damage > 0 && u.Damage >= Math.Max(MightOf(u.Id), 0)).ToList())
            Kill(unit.Id);
    }

    /// <summary>Ending special cleanup, 3c-3e: heal all units; "this turn" effects and Stunned expire; rune pools empty (CR 317.2).</summary>
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

    /// <summary>Combat special cleanup, 3c-3d: heal all units; recall attackers if defenders are still there (CR 466.1).</summary>
    private void CombatSteps()
    {
        HealAllUnits();
        if (State.Showdown is not { IsCombat: true } combat) return;
        var units = UnitsAt(Place.Battlefield(combat.Battlefield));
        if (!units.Any(u => u.Controller == combat.Defender)) return;
        foreach (var attacker in units.Where(u => u.Controller == combat.Attacker)) Recall(attacker.Id);
    }

    /// <summary>Step 4: control is lost where the controller has no units, if Open and nothing is happening there (CR 190.4.c).</summary>
    private void LoseControlOfEmptyBattlefields()
    {
        if (IsClosed) return;
        foreach (var battlefield in State.Battlefields)
            if (battlefield.Controller is { } controller && !Busy(battlefield.Index)
                && !UnitsAt(Place.Battlefield(battlefield.Index)).Any(u => u.Controller == controller))
                LoseControl(battlefield.Index);
    }

    /// <summary>A showdown or combat is staged or running at this battlefield.</summary>
    private bool Busy(int battlefield) =>
        State.Showdown?.Battlefield == battlefield || State.StagedShowdowns.Contains(battlefield) || State.StagedCombats.Contains(battlefield);

    /// <summary>Step 5: unattached gear and runes at battlefields go to Base; permanents in another player's Base go home;
    /// facedown cards at battlefields their controller doesn't control go to their owner's trash.</summary>
    private void RecallStrays()
    {
        foreach (var battlefield in State.Battlefields)
        {
            foreach (var id in State.At(Place.Battlefield(battlefield.Index)).ToList())
                if (!IsUnit(State[id]) && State[id].AttachedTo is null) Recall(id);
            foreach (var id in State.At(Place.Facedown(battlefield.Index)).ToList())
                if (State[id].Controller != battlefield.Controller) MoveCard(id, Place.Trash(State[id].Owner));
        }
        foreach (var player in State.Players)
            foreach (var id in State.At(Place.Base(player.Id)).ToList())
                if (State[id].Controller != player.Id) Recall(id);
    }

    /// <summary>Steps 6-7: stage a showdown where Contested was applied and the applier has units there; stage a combat
    /// where two players have units. Un-stage what no longer applies.</summary>
    private void StageShowdownsAndCombats()
    {
        foreach (var battlefield in State.Battlefields)
        {
            var units = UnitsAt(Place.Battlefield(battlefield.Index));
            var running = State.Showdown?.Battlefield == battlefield.Index;
            var showdown = !running && battlefield.ContestedBy is { } applier && units.Any(u => u.Controller == applier);
            var combat = units.Select(u => u.Controller).Distinct().Count() > 1 && !(running && State.Showdown!.IsCombat);
            if (Toggle(State.StagedShowdowns, battlefield.Index, showdown) | Toggle(State.StagedCombats, battlefield.Index, combat))
                MarkDirty();
        }
    }

    private static bool Toggle(SortedSet<int> set, int value, bool on) => on ? set.Add(value) : set.Remove(value);

    /// <summary>Step 8: clear Contested where the applier has no units and nothing is happening; 8a: units left on a battlefield
    /// their controller doesn't control re-apply it.</summary>
    private void ClearContested()
    {
        foreach (var battlefield in State.Battlefields)
        {
            var units = UnitsAt(Place.Battlefield(battlefield.Index));
            if (battlefield.ContestedBy is { } applier && !units.Any(u => u.Controller == applier) && !Busy(battlefield.Index))
            {
                battlefield.ContestedBy = null;
                MarkDirty();
            }
            if (battlefield.ContestedBy is null && units.FirstOrDefault(u => u.Controller != battlefield.Controller) is { } stranger)
                ApplyContested(stranger.Controller, battlefield.Index);
        }
    }

    /// <summary>Steps 9-10: in Neutral Open, start a staged showdown (where no combat is staged), else a staged combat; the turn
    /// player chooses when there are several. 10a: during a non-combat showdown, a combat staged there converts it.</summary>
    private bool StartStaged()
    {
        if (IsClosed) return true;
        if (State.Showdown is { IsCombat: false } running && State.StagedCombats.Contains(running.Battlefield))
        {
            ConvertToCombat(running);
            return true;
        }
        if (State.Showdown is not null) return true;

        var showdowns = State.StagedShowdowns.Where(b => !State.StagedCombats.Contains(b)).ToList();
        var combat = showdowns.Count == 0;
        var candidates = combat ? State.StagedCombats.ToList() : showdowns;
        if (candidates.Count == 0) return true;
        if (candidates.Count == 1)
        {
            Begin(candidates[0], combat);
            return true;
        }
        Ask(new ChooseShowdownDecision(State.Turn.TurnPlayer, candidates, combat), (_, action) =>
        {
            if (action is not ChooseShowdown choice || !candidates.Contains(choice.Battlefield))
                return Reject(RejectionCode.UnexpectedAction, "Choose one of the staged battlefields.");
            Begin(choice.Battlefield, combat);
            return null;
        });
        return false;
    }

    private void Begin(int battlefield, bool combat)
    {
        if (combat) StartCombat(battlefield);
        else StartShowdown(battlefield);
    }
}
