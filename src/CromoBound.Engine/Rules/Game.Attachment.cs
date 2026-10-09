using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Attaches gear to a unit (spec §8.2, CR 818): the gear joins the unit's location.</summary>
    internal void Attach(ObjectId gear, ObjectId unit)
    {
        var instance = State[gear];
        instance.AttachedTo = unit;
        if (instance.Place != State[unit].Place) MoveCard(gear, State[unit].Place);
        Emit(new Attached(gear, unit));
        MarkDirty();
    }

    /// <summary>The gear stays where it is; at a battlefield, cleanup step 5 recalls it to its controller's Base.</summary>
    internal void Detach(ObjectId gear)
    {
        State[gear].AttachedTo = null;
        Emit(new Detached(gear));
        MarkDirty();
    }

    /// <summary>After a unit moved: gear attached to it follows when it stayed on the board, and is detached and recalled to its
    /// controller's Base when it left (spec §8.2).</summary>
    private void MoveAttachments(ObjectId unit, Place landed, bool stayed)
    {
        foreach (var gear in State.Objects.Where(o => o.AttachedTo == unit).Select(o => o.Id).ToList())
        {
            if (stayed)
            {
                if (State[gear].Place != landed) MoveCard(gear, landed);
                continue;
            }
            Detach(gear);
            var home = Place.Base(State[gear].Controller);
            if (State[gear].Place != home) MoveCard(gear, home);
        }
    }

    private Rejection? AttachByHand(ManualAttach attach)
    {
        if (OnBoard(attach.Gear) is not { } gear || CardOf(gear).Type != CardType.Gear || BoardUnit(attach.Unit) is null)
            return Reject(RejectionCode.UnknownObject, "Choose gear in play and a unit in play.");
        Attach(attach.Gear, attach.Unit);
        return null;
    }

    private Rejection? DetachByHand(ManualDetach detach)
    {
        if (OnBoard(detach.Gear) is not { AttachedTo: not null })
            return Reject(RejectionCode.UnknownObject, "Choose gear in play that is attached to a unit.");
        Detach(detach.Gear);
        return null;
    }
}
