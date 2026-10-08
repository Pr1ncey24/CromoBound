using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Combat step 2 (CR 465): the attacker assigns, then the defender, then all damage is dealt at once.</summary>
internal sealed class CombatDamageTask(int battlefield) : GameTask
{
    public int Battlefield { get; } = battlefield;

    /// <summary>0: attacker assigns; 1: defender assigns; 2: deal.</summary>
    public int Step { get; set; }

    public List<DamageAssignment> Assignments { get; } = [];

    public override bool Run(Game game) => game.RunCombatDamage(this);
}

/// <summary>Combat step 3 (CR 466): combat cleanup, result, control, end of combat.</summary>
internal sealed class CombatResolutionTask(int battlefield) : GameTask
{
    public int Battlefield { get; } = battlefield;
    public bool CleanedUp { get; set; }

    public override bool Run(Game game) => game.RunCombatResolution(this);
}

public sealed partial class Game
{
    /// <summary>Attacker = the player who applied Contested; they get focus (CR 464).</summary>
    private void StartCombat(int battlefield)
    {
        State.StagedCombats.Remove(battlefield);
        State.StagedShowdowns.Remove(battlefield);
        var attacker = State.Battlefields[battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        var defender = State.Opponent(attacker);
        State.Showdown = new ShowdownState(battlefield) { IsCombat = true, Attacker = attacker, Defender = defender };
        State.Turn.Focus = attacker;
        State.Turn.Priority = attacker;
        Emit(new CombatStarted(battlefield, attacker, defender));
        MarkDirty();
    }

    /// <summary>Cleanup step 10a: enemy units arrived during a non-combat showdown; it becomes a combat showdown and focus stays put.</summary>
    private void ConvertToCombat(ShowdownState showdown)
    {
        State.StagedCombats.Remove(showdown.Battlefield);
        var attacker = State.Battlefields[showdown.Battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        showdown.IsCombat = true;
        showdown.Attacker = attacker;
        showdown.Defender = State.Opponent(attacker);
        Emit(new CombatStarted(showdown.Battlefield, attacker, showdown.Defender.Value));
        MarkDirty();
    }

    internal bool RunCombatDamage(CombatDamageTask task)
    {
        var combat = State.Showdown!;
        var units = UnitsAt(Place.Battlefield(task.Battlefield));
        var attackers = units.Where(u => u.Controller == combat.Attacker).ToList();
        var defenders = units.Where(u => u.Controller == combat.Defender).ToList();
        if (task.Step == 0 && (attackers.Count == 0 || defenders.Count == 0)) task.Step = 2;
        if (task.Step == 0)
        {
            if (AskAssignment(task, combat.Attacker!.Value, attackers, defenders)) return false;
            task.Step = 1;
        }
        if (task.Step == 1)
        {
            if (AskAssignment(task, combat.Defender!.Value, defenders, attackers)) return false;
            task.Step = 2;
        }
        foreach (var assignment in task.Assignments) DealDamage(assignment.Unit, assignment.Amount);
        Push(new CombatResolutionTask(task.Battlefield));
        return true;
    }

    /// <summary>Asks a side to assign its damage; false when it has none to deal or nothing to hit. Stunned units deal 0 (CR 423.1.b).</summary>
    private bool AskAssignment(CombatDamageTask task, PlayerId player, List<CardInstance> own, List<CardInstance> enemies)
    {
        var total = own.Sum(u => u.Stunned ? 0 : Math.Max(MightOf(u.Id), 0));
        var targets = enemies
            .Select(u => new DamageTarget(
                u.Id,
                CombatDamage.Lethal(MightOf(u.Id), u.Damage),
                CombatDamage.GroupOf(Has(u, DisplayKeyword.Tank), Has(u, DisplayKeyword.Backline))))
            .ToList();
        if (total == 0 || targets.Count == 0) return false;
        Ask(new AssignDamageDecision(player, task.Battlefield, total, targets, CombatDamage.Suggest(targets, total)), (_, action) =>
        {
            if (action is not AssignDamage assign) return Reject(RejectionCode.UnexpectedAction, "Assign your combat damage.");
            if (CombatDamage.Validate(targets, total, assign.Assignments) is { } error) return Reject(RejectionCode.InvalidAssignment, error);
            task.Assignments.AddRange(assign.Assignments.Where(a => a.Amount > 0));
            task.Step++;
            return null;
        });
        return true;
    }

    internal bool RunCombatResolution(CombatResolutionTask task)
    {
        if (!task.CleanedUp)
        {
            task.CleanedUp = true;
            Push(new CleanupTask(CleanupMode.Combat));
            return false;
        }
        var combat = State.Showdown!;
        var units = UnitsAt(Place.Battlefield(task.Battlefield));
        var attackerLeft = units.Any(u => u.Controller == combat.Attacker);
        var defenderLeft = units.Any(u => u.Controller == combat.Defender);
        var result = attackerLeft && !defenderLeft ? CombatResult.AttackerWon
            : defenderLeft && !attackerLeft ? CombatResult.DefenderWon
            : CombatResult.NoResult;

        State.Showdown = null;
        State.Turn.Focus = null;
        State.Turn.Priority = null;
        foreach (var unit in BoardUnits().ToList())
        {
            unit.Role = null;
            unit.Modifiers.RemoveAll(m => m.Duration == Duration.ThisCombat);
        }
        Emit(new CombatEnded(task.Battlefield, result));

        var present = units.Select(u => u.Controller).Distinct().ToList();
        if (present.Count == 1)
        {
            EstablishControl(present[0], task.Battlefield);
        }
        else if (present.Count == 0)
        {
            LoseControl(task.Battlefield);
            State.Battlefields[task.Battlefield].ContestedBy = null;
        }
        MarkDirty();
        return true;
    }
}
