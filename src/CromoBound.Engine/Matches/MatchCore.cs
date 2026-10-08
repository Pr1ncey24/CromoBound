using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Random;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>A match's state and the rules around its games. Undo replaces it with a fresh replay.</summary>
internal sealed partial class MatchCore
{
    public static readonly IReadOnlyList<PlayerId> Players = [new(0), new(1)];

    private readonly List<GameEvent> _newEvents = [];
    private PendingDecision? _pending;
    private Func<PlayerId, PlayerAction, Rejection?>? _handler;
    private int _nextSequence = 1;

    public MatchCore(MatchSetup setup, CardDatabase db)
    {
        Setup = setup;
        Db = db;
        Rng = new SeededRandom(setup.Seed);
        Decks = [setup.Player1Deck, setup.Player2Deck];
        Available = [[.. setup.Player1Deck.Battlefields], [.. setup.Player2Deck.Battlefields]];
        BeginGame();
        Flush();
    }

    public MatchSetup Setup { get; }
    public CardDatabase Db { get; }

    /// <summary>One generator for the whole match, shared with every game, so a replay is identical.</summary>
    public SeededRandom Rng { get; }

    /// <summary>Current decks (after sideboarding), by player index. The registered decks stay in <see cref="Setup"/>.</summary>
    public Deck[] Decks { get; }

    /// <summary>Battlefield printings each player may still use this match.</summary>
    public List<string>[] Available { get; }

    public int[] Wins { get; } = new int[2];
    public string?[] Picks { get; } = new string?[2];
    public int GameNumber { get; private set; }
    public MatchStage Stage { get; private set; }
    public Game? Game { get; private set; }
    public PlayerId First { get; private set; }
    public PlayerId? LastLoser { get; private set; }
    public PlayerId? Winner { get; private set; }
    public PlayerId? UndoRequestedBy { get; set; }

    /// <summary>Log index of the current game's first action after the mulligans; -1 before play starts.</summary>
    public int PlayStartIndex { get; private set; } = -1;

    public List<LoggedAction> Log { get; } = [];

    /// <summary>Every event of the match, numbered match-wide.</summary>
    public List<GameEvent> Events { get; } = [];

    public MatchResult Result => new([.. Wins], Winner);

    public PendingDecision? Pending =>
        UndoRequestedBy is { } requester ? new ConfirmUndoDecision(Opponent(requester), requester)
        : _pending ?? (Stage == MatchStage.Playing ? Game!.Pending : null);

    public static PlayerId Opponent(PlayerId player) => new(1 - player.Index);

    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        if (ActionShape.Check(action) is { } malformed) return SubmitResult.Reject(RejectionCode.UnexpectedAction, malformed);
        if (!Players.Contains(player)) return SubmitResult.Reject(RejectionCode.NotYourDecision, $"{player} is not in this match.");
        if (Winner is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The match is over.");
        if (UndoRequestedBy is not null && action is not Concede)
            return SubmitResult.Reject(RejectionCode.UnexpectedAction, "Answer the undo request first.");

        var rejection = action switch
        {
            Concede => ConcedeGame(player),
            ManualAction manual => ApplyManual(player, manual),
            _ when _pending is not null => AnswerMatchDecision(player, action),
            _ when Stage == MatchStage.Playing => SubmitToGame(player, action),
            _ => Reject(RejectionCode.UnexpectedAction, "Nothing is waiting for that action."),
        };
        if (rejection is not null)
        {
            _newEvents.Clear();
            return new SubmitResult(false, rejection, []);
        }
        Log.Add(new LoggedAction(player, action));
        if (Stage == MatchStage.Playing && PlayStartIndex < 0) PlayStartIndex = Log.Count;
        return new SubmitResult(true, null, Flush());
    }

    internal static Rejection Reject(RejectionCode code, string message) => new(code, message);

    /// <summary>Adds a match event after any game events produced before it, keeping the order.</summary>
    internal void Emit(GameEvent gameEvent)
    {
        PullGameEvents();
        _newEvents.Add(gameEvent);
    }

    private void PullGameEvents()
    {
        if (Game is not null) _newEvents.AddRange(Game.TakeEvents());
    }

    /// <summary>Numbers the new events match-wide, keeps them, and returns them.</summary>
    private IReadOnlyList<GameEvent> Flush()
    {
        PullGameEvents();
        foreach (var gameEvent in _newEvents) gameEvent.Sequence = _nextSequence++;
        Events.AddRange(_newEvents);
        var result = _newEvents.ToList();
        _newEvents.Clear();
        return result;
    }

    private void Ask(PendingDecision decision, Func<PlayerId, PlayerAction, Rejection?> handler)
    {
        _pending = decision;
        _handler = handler;
    }

    private Rejection? AnswerMatchDecision(PlayerId player, PlayerAction action)
    {
        if (!_pending!.Players.Contains(player)) return Reject(RejectionCode.NotYourDecision, $"{player} is not deciding now.");
        var (decision, handler) = (_pending, _handler!);
        _pending = null;
        _handler = null;
        var rejection = handler(player, action);
        if (rejection is not null && _pending is null) (_pending, _handler) = (decision, handler);
        return rejection;
    }

    private Rejection? SubmitToGame(PlayerId player, PlayerAction action)
    {
        var result = Game!.Submit(player, action);
        if (!result.Accepted) return result.Rejection;
        _newEvents.AddRange(result.Events);
        AfterGameChange();
        return null;
    }

    /// <summary>Manual actions are possible only while a game is being played (not during pre-game steps).</summary>
    private Rejection? ApplyManual(PlayerId player, ManualAction action)
    {
        if (Stage != MatchStage.Playing) return Reject(RejectionCode.WrongTiming, "Manual actions are only possible during play.");
        var result = Game!.SubmitManual(player, action);
        if (!result.Accepted) return result.Rejection;
        _newEvents.AddRange(result.Events);
        AfterGameChange();
        return null;
    }
}
