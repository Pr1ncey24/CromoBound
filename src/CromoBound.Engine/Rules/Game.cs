using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>Runs one game: validates actions against the pending decision and applies the rules until someone must decide.</summary>
public sealed partial class Game
{
    public const int VictoryScore = 8;

    private readonly List<GameTask> _tasks = [];
    private readonly List<GameEvent> _events = [];
    private Func<PlayerId, PlayerAction, Rejection?>? _handler;
    private int _nextSequence = 1;
    private bool _cleanupNeeded;
    private readonly Dictionary<string, IReadOnlySet<DisplayKeyword>> _ownKeywords = [];

    public Game(GameState state, CardDatabase db)
    {
        State = state;
        Db = db;
    }

    public GameState State { get; }
    public CardDatabase Db { get; }

    /// <summary>What the engine waits for. Null only when the game is over.</summary>
    public PendingDecision? Pending { get; private set; }

    public GameOutcome? Outcome { get; private set; }

    /// <summary>Counts board changes; a cleanup repeats until a pass leaves it unchanged (CR 322).</summary>
    internal int Changes { get; private set; }

    /// <summary>True while a chain item is resolved by hand; cleanups wait until it's done (CR 321).</summary>
    internal bool ResolvingManually { get; set; }

    /// <summary>Starts the first turn and runs until the first decision.</summary>
    public IReadOnlyList<GameEvent> Start(PlayerId firstPlayer)
    {
        Enqueue(new StepTask(g => g.StartTurn(firstPlayer)));
        return Continue();
    }

    /// <summary>Runs the rules until a decision is needed and returns the events produced since the last call.</summary>
    public IReadOnlyList<GameEvent> Continue()
    {
        RunLoop();
        return TakeEvents();
    }

    /// <summary>Events produced since the last call (e.g. by setup draws before <see cref="Start"/>).</summary>
    internal IReadOnlyList<GameEvent> TakeEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        if (ActionShape.Check(action) is { } malformed) return SubmitResult.Reject(RejectionCode.UnexpectedAction, malformed);
        if (Outcome is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The game is over.");
        if (Pending is null || _handler is null) throw new InvalidOperationException("The game is running but nothing is pending.");
        if (!Pending.Players.Contains(player)) return SubmitResult.Reject(RejectionCode.NotYourDecision, $"{player} is not the one deciding now.");

        var (decision, handler) = (Pending, _handler);
        Pending = null;
        _handler = null;
        Rejection? rejection;
        try
        {
            rejection = handler(player, action);
        }
        catch
        {
            if (Pending is null) (Pending, _handler) = (decision, handler);
            throw;
        }
        if (rejection is not null)
        {
            if (Pending is null) (Pending, _handler) = (decision, handler);
            return new SubmitResult(false, rejection, []);
        }
        return new SubmitResult(true, null, Continue());
    }

    /// <summary>Current Might: printed + buff + active modifiers (spec §5).</summary>
    public int MightOf(ObjectId id)
    {
        var unit = State[id];
        return (CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount);
    }

    /// <summary>Raises a decision. The handler validates the answer first, then applies it; it returns a rejection to keep waiting.</summary>
    internal void Ask(PendingDecision decision, Func<PlayerId, PlayerAction, Rejection?> handler)
    {
        Pending = decision;
        _handler = handler;
    }

    internal static Rejection Reject(RejectionCode code, string message) => new(code, message);

    internal void Emit(GameEvent gameEvent)
    {
        gameEvent.Sequence = _nextSequence++;
        _events.Add(gameEvent);
    }

    internal void Enqueue(GameTask task) => _tasks.Add(task);

    /// <summary>Runs <paramref name="task"/> before every other queued task.</summary>
    internal void Push(GameTask task) => _tasks.Insert(0, task);

    /// <summary>Records a board change: a cleanup is now outstanding (CR 319).</summary>
    internal void MarkDirty()
    {
        Changes++;
        _cleanupNeeded = true;
    }

    internal void CleanupDone() => _cleanupNeeded = false;

    internal void End(PlayerId? winner, GameEndReason reason)
    {
        if (Outcome is not null) return;
        Outcome = new GameOutcome(winner, reason);
        Pending = null;
        _handler = null;
        Emit(new GameEnded(winner, reason));
    }

    internal Card CardOf(CardInstance instance) => Db.Cards[instance.CardId];

    internal Card CardOf(ObjectId id) => CardOf(State[id]);

    /// <summary>Whether the card itself has the keyword (spec §7.10): only keywords starting a text line count, see <see cref="CardKeywords"/>.</summary>
    internal bool Has(CardInstance instance, DisplayKeyword keyword)
    {
        if (!_ownKeywords.TryGetValue(instance.CardId, out var own))
            _ownKeywords[instance.CardId] = own = CardKeywords.Own(CardOf(instance));
        return own.Contains(keyword);
    }

    internal bool IsUnit(CardInstance instance) => CardOf(instance).Type == CardType.Unit;

    internal List<CardInstance> UnitsAt(Place location) => [.. State.At(location).Select(id => State[id]).Where(IsUnit)];

    internal IEnumerable<CardInstance> BoardUnits() => State.Objects.Where(o => o.Place.IsLocation && IsUnit(o));

    internal List<RuneInfo> RunesOf(PlayerId player) =>
    [
        .. State.At(Place.Base(player)).Select(id => State[id])
            .Where(o => o.Controller == player && CardOf(o).Type == CardType.Rune)
            .Select(o => new RuneInfo(o.Id, CardOf(o).Domains[0], o.Exhausted)),
    ];

    internal bool HasWon(PlayerId player)
    {
        var points = State.Player(player).Points;
        return points >= VictoryScore && State.Players.All(p => p.Id == player || p.Points < points);
    }

    /// <summary>Handle outstanding tasks, then the chain, the showdown, the Main phase (CR 334-336).
    /// A turn-structure task (<see cref="GameTask.WaitsForNeutralOpen"/>) waits while a chain or showdown is running,
    /// even after it started, and a cleanup may run while such a task is paused on its decision.</summary>
    private void RunLoop()
    {
        while (Outcome is null && Pending is null)
        {
            var head = _tasks.Count > 0 ? _tasks[0] : null;
            var paused = head is { WaitsForNeutralOpen: true } && (IsClosed || State.Showdown is not null);
            if (_cleanupNeeded && !ResolvingManually && (head is null || !head.Started || head.WaitsForNeutralOpen))
            {
                _cleanupNeeded = false;
                Push(new CleanupTask(CleanupMode.Normal));
                continue;
            }
            if (head is not null && !paused)
            {
                head.Started = true;
                if (head.Run(this)) _tasks.Remove(head);
                continue;
            }
            AskPriority();
        }
    }
}
