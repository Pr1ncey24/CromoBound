using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

/// <summary>A deterministic scripted player: plays the first affordable unit to its Base, sends ready units from Base to a
/// battlefield it doesn't control, passes whenever it can, resolves by hand immediately and takes every suggestion.</summary>
internal sealed class Bot
{
    private readonly HashSet<string> _unaffordable = [];
    private int _turn;
    private string? _playing;

    /// <summary>Pre-game: first offered battlefield, play first, no sideboard changes, no mulligan; in play, as for a game.</summary>
    public PlayerAction Choose(Match match) => match.Pending switch
    {
        PickBattlefieldDecision pick => new PickBattlefield(pick.Choices.Single(c => c.Player == pick.Players[0]).Printings[0]),
        ChoosePlayOrderDecision => new ChoosePlayOrder(true),
        SideboardDecision => new SubmitSideboard(),
        MulliganDecision => new Mulligan(),
        _ => Choose(match.Game!),
    };

    public PlayerAction Choose(Game game)
    {
        if (game.State.Turn.Number != _turn)
        {
            _turn = game.State.Turn.Number;
            _unaffordable.Clear();
        }
        return game.Pending switch
        {
            PriorityDecision priority => ChoosePriority(game, priority),
            PlayChoicesDecision choices => new ChoosePlayOptions(choices.Locations.Count > 0 ? choices.Locations[0] : null, false),
            PayCostDecision { Suggested: { } s } => new PayCost { Exhaust = s.Exhaust, Recycle = s.Recycle },
            PayCostDecision => GiveUp(),
            AssignDamageDecision assign => new AssignDamage { Assignments = assign.Suggested },
            ChooseShowdownDecision showdown => new ChooseShowdown(showdown.Battlefields[0]),
            ResolveManuallyDecision => new ResolveDone(),
            TurnPointDecision => new ContinueTurn(),
            _ => throw new InvalidOperationException($"Unexpected decision {game.Pending}."),
        };
    }

    private PlayerAction GiveUp()
    {
        _unaffordable.Add(_playing!);
        return new CancelPlay();
    }

    private PlayerAction ChoosePriority(Game game, PriorityDecision priority)
    {
        if (priority.CanPass) return new Pass();
        var state = game.State;
        var unit = priority.Playable.FirstOrDefault(id =>
            game.Db.Cards[state[id].CardId].Type == CardType.Unit && !_unaffordable.Contains(state[id].CardId));
        if (unit != default)
        {
            _playing = state[unit].CardId;
            return new PlayCard(unit);
        }
        var target = state.Battlefields
            .Where(b => b.Controller != priority.Player)
            .Select(b => Place.Battlefield(b.Index))
            .FirstOrDefault(Place.Base(priority.Player));
        var movers = priority.Moves
            .Where(m => state[m.Unit].Place.Kind == PlaceKind.Base && m.Destinations.Contains(target))
            .Select(m => m.Unit)
            .ToList();
        if (target.Kind == PlaceKind.Battlefield && movers.Count > 0)
            return new StandardMove { Units = movers, Destination = target };
        return new EndTurn();
    }
}
