using System.Text.RegularExpressions;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Pauses so start/end-of-turn effects can be applied by hand: the turn player first, then the opponent.</summary>
internal sealed class TurnPointTask(TurnPoint point) : GameTask
{
    public TurnPoint Point { get; } = point;
    public List<PlayerId>? Order { get; set; }
    public int Next { get; set; }

    internal override bool WaitsForNeutralOpen => true;

    public override bool Run(Game game) => game.RunTurnPoint(this);
}

public sealed partial class Game
{
    private static readonly Regex StartOfBeginningText = new(@"\bat (the )?(start|beginning) of\b[^.]*\bbeginning phase\b", RegexOptions.IgnoreCase);
    private static readonly Regex StartOfMainText = new(@"\bat (the )?(start|beginning) of\b[^.]*\bmain phase\b", RegexOptions.IgnoreCase);
    private static readonly Regex EndOfTurnText = new(@"\bat (the )?end of\b[^.]*\bturn\b", RegexOptions.IgnoreCase);
    private static readonly Regex Your = new(@"\byour\b", RegexOptions.IgnoreCase);

    internal bool RunTurnPoint(TurnPointTask task)
    {
        task.Order ??= [.. TurnOrder()];
        while (task.Next < task.Order.Count)
        {
            var player = task.Order[task.Next];
            var cards = TurnPointCards(task.Point, player);
            if (cards.Count == 0)
            {
                task.Next++;
                continue;
            }
            Ask(new TurnPointDecision(player, task.Point, cards), (_, action) =>
            {
                if (action is not ContinueTurn)
                    return Reject(RejectionCode.UnexpectedAction, "Apply the effects with manual actions, then continue the turn.");
                task.Next++;
                return null;
            });
            return false;
        }
        return true;
    }

    /// <summary>Cards in play handled by <paramref name="player"/> whose text the players still resolve (<see cref="TurnPointText"/>) acts at this point (reminder text ignored).
    /// Text saying "your" counts only on its controller's turn.</summary>
    internal List<ObjectId> TurnPointCards(TurnPoint point, PlayerId player)
    {
        var pattern = point switch
        {
            TurnPoint.StartOfBeginning => StartOfBeginningText,
            TurnPoint.StartOfMain => StartOfMainText,
            _ => EndOfTurnText,
        };
        var result = new List<ObjectId>();
        foreach (var instance in State.Objects.Where(o => o.Place.IsBoard && o.Place.Kind != PlaceKind.Facedown))
        {
            if (HandledBy(instance) != player) continue;
            if (TurnPointText(instance) is not { } text) continue;
            var match = pattern.Match(RichText.StripReminders(text));
            if (!match.Success) continue;
            if (Your.IsMatch(match.Value) && player != State.Turn.TurnPlayer) continue;
            result.Add(instance.Id);
        }
        return result;
    }

    /// <summary>The text that may need a turn-point pause (spec §9): all of an Unmapped card's text, only the manual lines of a
    /// Partial card, and none of a Full card's (the engine runs it).</summary>
    private string? TurnPointText(CardInstance instance)
    {
        var effects = Effects.For(instance.CardId);
        var text = CardOf(instance).Text.Rich;
        if (effects.Status == MappingStatus.Unmapped) return text;
        if (effects.Status == MappingStatus.Full || effects.ManualLines.Count == 0) return null;
        var lines = RichText.Lines(text);
        return string.Join("<br />", effects.ManualLines.Select(n => lines[n - 1]));
    }

    /// <summary>A battlefield's abilities belong to its controller, or the turn player when uncontrolled (CR 190.6).</summary>
    internal PlayerId HandledBy(CardInstance instance) =>
        instance.Place.Kind == PlaceKind.BattlefieldCard
            ? State.Battlefields[instance.Place.Index!.Value].Controller ?? State.Turn.TurnPlayer
            : instance.Controller;

    private IEnumerable<PlayerId> TurnOrder()
    {
        yield return State.Turn.TurnPlayer;
        foreach (var player in State.Players)
            if (player.Id != State.Turn.TurnPlayer) yield return player.Id;
    }
}
