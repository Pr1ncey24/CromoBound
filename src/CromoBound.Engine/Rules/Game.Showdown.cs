using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>A non-combat showdown: the player who applied Contested gets focus and priority (CR 345).</summary>
    private void StartShowdown(int battlefield)
    {
        State.StagedShowdowns.Remove(battlefield);
        var focus = State.Battlefields[battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        State.Showdown = new ShowdownState(battlefield);
        State.Turn.Focus = focus;
        State.Turn.Priority = focus;
        Emit(new ShowdownStarted(battlefield, focus));
        MarkDirty();
    }

    /// <summary>Passing in an open showdown hands focus on. When every player has passed in a row, it ends (CR 347-348).</summary>
    private void PassFocus(PlayerId player)
    {
        var showdown = State.Showdown!;
        showdown.Passes++;
        if (showdown.Passes >= State.Players.Count)
        {
            EndShowdown(showdown);
            return;
        }
        State.Turn.Focus = State.Opponent(player);
        State.Turn.Priority = State.Turn.Focus;
    }

    /// <summary>A combat showdown continues with combat damage. A non-combat showdown gives control to the only player with
    /// units there (a Conquer if not scored this turn); with no units left, the battlefield is left uncontested (spec §13 #4).</summary>
    private void EndShowdown(ShowdownState showdown)
    {
        if (showdown.IsCombat)
        {
            Push(new CombatDamageTask(showdown.Battlefield));
            return;
        }
        State.Showdown = null;
        State.Turn.Focus = null;
        State.Turn.Priority = null;
        Emit(new ShowdownEnded(showdown.Battlefield));
        var battlefield = State.Battlefields[showdown.Battlefield];
        var present = UnitsAt(Place.Battlefield(showdown.Battlefield)).Select(u => u.Controller).Distinct().ToList();
        if (present.Count == 1)
        {
            EstablishControl(present[0], battlefield.Index);
            return;
        }
        battlefield.ContestedBy = null;
        MarkDirty();
    }
}
