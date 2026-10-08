using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Hide (CR 421, 811): pay [A], then the card goes face down at a battlefield you control.</summary>
internal sealed class HideTask(PlayerId player, ObjectId card, int battlefield) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Card { get; } = card;
    public int Battlefield { get; } = battlefield;
    public bool Paid { get; set; }
    public bool Cancelled { get; set; }
    public TotalCost Cost { get; set; } = new(0, [PowerSymbol.Any]);

    public override bool Run(Game game) => game.RunHide(this);
}

public sealed partial class Game
{
    private void StartHide(PlayerId player, ObjectId card, int battlefield) => Push(new HideTask(player, card, battlefield));

    internal bool RunHide(HideTask task)
    {
        if (task.Cancelled) return true;
        if (!task.Paid)
        {
            AskPay(task.Player, task.Cost, CardOf(task.Card).Domains,
                onPaid: () => task.Paid = true,
                onCancel: () => task.Cancelled = true,
                onAdjust: adjusted => task.Cost = adjusted);
            return false;
        }
        var id = MoveCard(task.Card, Place.Facedown(task.Battlefield))!.Value;
        var card = State[id];
        card.Facedown = true;
        card.Controller = task.Player;
        card.HiddenOnTurn = State.Turn.Number;
        return true;
    }
}
