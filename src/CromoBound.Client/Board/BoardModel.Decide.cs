using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    private partial void Decide(PlayerView view, Build build, Interaction interaction)
    {
        switch (view.Decision)
        {
            case PriorityDecision priority:
                if (interaction is not Moving moving || !Move(priority, build, moving)) Priority(priority, build);
                break;
            case PlayChoicesDecision choices:
                Panel = new PlayOptionsPanel(build.Book.NameOf(build.Find(choices.Card)?.CardId ?? ""),
                    [.. choices.Locations.Select(l => new PlaceOption(l, PlaceName(l, build)))], choices.AccelerateAvailable);
                break;
            case PayCostDecision pay:
                Pay(pay, build, interaction as Paying);
                break;
            default:
                Other(view, build);
                break;
        }
    }

    /// <summary>Each card's options: one option is a click, several open a menu. A rune always opens its menu, so a stray click never
    /// spends it.</summary>
    private void Priority(PriorityDecision priority, Build build)
    {
        var items = new Dictionary<ObjectId, List<MenuItem>>();
        void Add(ObjectId card, string label, BoardStep step)
        {
            if (!items.TryGetValue(card, out var list)) items[card] = list = [];
            list.Add(new MenuItem(label, step));
        }

        foreach (var card in priority.Playable)
        {
            Add(card, "Play", new SendStep(new PlayCard(card)));
            _rings[card] = Ring.Legal;
        }
        foreach (var move in priority.Moves) Add(move.Unit, "Move", new NextStep(new Moving([move.Unit])));
        foreach (var hide in priority.Hides)
            foreach (var battlefield in hide.Battlefields)
                Add(hide.Card, $"Hide at {LaneName(battlefield, build)}", new SendStep(new Hide(hide.Card, battlefield)));
        foreach (var activation in priority.Activations)
            Add(activation.Source, $"Use ability {activation.Ability + 1}", new SendStep(new ActivateAbility(activation.Source, activation.Ability)));
        var runes = priority.Runes.Select(r => r.Rune).ToHashSet();
        foreach (var rune in priority.Runes)
        {
            if (rune.CanExhaust) Add(rune.Rune, "Exhaust for 1 energy", new SendStep(new UseRune(rune.Rune, RuneUse.Exhaust)));
            Add(rune.Rune, "Recycle for 1 power", new SendStep(new UseRune(rune.Rune, RuneUse.Recycle)));
        }
        foreach (var (card, list) in items)
            _cardSteps[card] = list.Count == 1 && !runes.Contains(card) ? list[0].Step : new MenuStep(card, list);

        Button = priority.CanPass ? new BoardButton("PASS", null, new SendStep(new Pass()))
            : priority.CanEndTurn ? new BoardButton("END TURN", null, new SendStep(new EndTurn()))
            : new BoardButton("END TURN", null, NoStep.Instance);
    }

    /// <summary>A move under way: only destinations every chosen unit shares; another unit that shares one may join; clicking a
    /// chosen unit drops it (the first one drops the whole move). False when none of the units can move any more.</summary>
    private bool Move(PriorityDecision priority, Build build, Moving moving)
    {
        var options = priority.Moves.ToDictionary(m => m.Unit);
        var units = moving.Units.Where(options.ContainsKey).ToList();
        if (units.Count == 0) return false;
        var shared = units.Skip(1).Aggregate((IEnumerable<Place>)options[units[0]].Destinations, (d, u) => d.Intersect(options[u].Destinations)).ToList();
        if (shared.Count == 0) return false;

        foreach (var unit in units)
        {
            _rings[unit] = Ring.Selected;
            _cardSteps[unit] = new NextStep(unit == units[0] ? Idle.Instance : new Moving([.. units.Where(u => u != unit)]));
        }
        foreach (var (unit, option) in options)
        {
            if (units.Contains(unit) || !option.Destinations.Intersect(shared).Any()) continue;
            _cardSteps[unit] = new NextStep(new Moving([.. units, unit]));
            _rings[unit] = Ring.Legal;
        }
        foreach (var destination in shared)
        {
            var step = new SendStep(new StandardMove { Units = units, Destination = destination });
            if (destination.Kind == PlaceKind.Battlefield && destination.Index is { } index)
            {
                _laneSteps[index] = step;
                _destinations.Add(index);
            }
            else if (destination.Kind == PlaceKind.Base)
            {
                _baseStep = step;
                _baseIsDestination = true;
            }
        }
        _cancel = new NextStep(Idle.Instance);
        Hint = units.Count == 1
            ? $"Moving {build.Book.NameOf(build.Find(units[0])?.CardId ?? "")}: pick a destination"
            : $"Moving {units.Count} units: pick a destination";
        Button = new BoardButton("END TURN", null, NoStep.Instance);
        return true;
    }

    /// <summary>Paying: each of the player's runes cycles exhaust, recycle, unused (an exhausted rune skips exhaust); the engine's
    /// suggestion is the starting point and what Suggest puts back.</summary>
    private void Pay(PayCostDecision pay, Build build, Paying? paying)
    {
        var suggested = Suggestion(pay);
        var uses = paying?.Uses ?? suggested;
        var runes = build.View.Players[build.View.Viewer.Index].Base.Where(c => build.Book.Card(c.CardId)?.Type == CardType.Rune).ToList();
        foreach (var rune in runes)
        {
            RuneUse? use = uses.TryGetValue(rune.Id, out var chosen) ? chosen : null;
            _marks[rune.Id] = use switch { RuneUse.Exhaust => PayMark.Exhaust, RuneUse.Recycle => PayMark.Recycle, _ => PayMark.None };
            if (suggested.ContainsKey(rune.Id)) _rings[rune.Id] = Ring.Suggested;
            _cardSteps[rune.Id] = new NextStep(new Paying(Cycle(uses, rune.Id, use, rune.Exhausted)));
        }
        List<ObjectId> With(RuneUse wanted) => [.. runes.Where(r => uses.TryGetValue(r.Id, out var u) && u == wanted).Select(r => r.Id)];

        Button = new BoardButton("PAY", CostText(pay.Cost), new SendStep(new PayCost { Exhaust = With(RuneUse.Exhaust), Recycle = With(RuneUse.Recycle) }));
        var cancel = new BoardButton("Cancel", null, new SendStep(new CancelPlay()));
        Extras = pay.Suggested is null ? [cancel] : [cancel, new BoardButton("Suggest", null, new NextStep(new Paying(suggested)))];
    }

    public static string CostText(TotalCost cost)
    {
        var parts = new List<string>();
        if (cost.Energy > 0) parts.Add($"{cost.Energy} energy");
        foreach (var symbol in cost.Power.GroupBy(p => p))
            parts.Add(symbol.Key switch
            {
                PowerSymbol.Any => $"{symbol.Count()} of any domain",
                PowerSymbol.Self => $"{symbol.Count()} of its domain",
                _ => $"{symbol.Count()} {symbol.Key}",
            });
        return parts.Count switch
        {
            0 => "Free",
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    private static Dictionary<ObjectId, RuneUse> Suggestion(PayCostDecision pay)
    {
        var uses = new Dictionary<ObjectId, RuneUse>();
        if (pay.Suggested is not { } suggested) return uses;
        foreach (var rune in suggested.Exhaust) uses[rune] = RuneUse.Exhaust;
        foreach (var rune in suggested.Recycle) uses[rune] = RuneUse.Recycle;
        return uses;
    }

    private static Dictionary<ObjectId, RuneUse> Cycle(IReadOnlyDictionary<ObjectId, RuneUse> uses, ObjectId rune, RuneUse? current, bool exhausted)
    {
        var next = new Dictionary<ObjectId, RuneUse>(uses);
        RuneUse? after = current switch
        {
            null => exhausted ? RuneUse.Recycle : RuneUse.Exhaust,
            RuneUse.Exhaust => RuneUse.Recycle,
            _ => null,
        };
        if (after is { } use) next[rune] = use;
        else next.Remove(rune);
        return next;
    }

    private static string LaneName(int index, Build build) =>
        build.View.Battlefields.FirstOrDefault(b => b.Index == index) is { } lane ? build.Book.NameOf(lane.Card.CardId) : $"battlefield {index + 1}";

    private static string PlaceName(Place place, Build build) => place.Kind switch
    {
        PlaceKind.Base => "Your base",
        PlaceKind.Battlefield when place.Index is { } index => LaneName(index, build),
        _ => place.Kind.ToString(),
    };
}
