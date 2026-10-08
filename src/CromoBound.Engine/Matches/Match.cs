using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>A Bo1 or Bo3 match between two players (spec §4). The server's entry point to the engine.</summary>
public sealed partial class Match
{
    private MatchCore _core;

    /// <summary>What <paramref name="player"/> may see: the only thing the server should send to that player (spec §10).</summary>
    public PlayerView ViewFor(PlayerId player) => ViewBuilder.Build(_core, player);

    private Match(MatchCore core) => _core = core;

    /// <summary>Validates both decks; returns no match when either is illegal.</summary>
    public static MatchCreateResult Create(MatchSetup setup, CardDatabase db)
    {
        DeckReport[] reports = [DeckValidator.Validate(setup.Player1Deck, db), DeckValidator.Validate(setup.Player2Deck, db)];
        return reports.All(r => r.IsLegal) ? new(new Match(new MatchCore(setup, db)), reports) : new(null, reports);
    }

    public PendingDecision? Pending => _core.Pending;
    public MatchResult Result => _core.Result;
    public MatchStage Stage => _core.Stage;
    public int GameNumber => _core.GameNumber;

    /// <summary>The current game, for the server. Players see <c>ViewFor</c>.</summary>
    public Game? Game => _core.Game;

    /// <summary>Every event of the match, unfiltered. Server-side only; players see their view's log.</summary>
    public IReadOnlyList<GameEvent> Events => _core.Events;

    /// <summary>Each player's deck after sideboarding.</summary>
    public IReadOnlyList<Deck> CurrentDecks => _core.Decks;

    /// <summary>Submits an action. Undo requests and answers are handled here, because an accepted undo replaces the whole core.
    /// If anything throws, the core is rebuilt from the log (which holds only accepted actions) so the live state cannot drift from it.</summary>
    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        if (ActionShape.Check(action) is { } malformed) return SubmitResult.Reject(RejectionCode.UnexpectedAction, malformed);
        if (!MatchCore.Players.Contains(player)) return SubmitResult.Reject(RejectionCode.NotYourDecision, $"{player} is not in this match.");
        try
        {
            return Route(player, action);
        }
        catch
        {
            _core = MatchCore.Replay(_core.Setup, _core.Db, _core.Log);
            throw;
        }
    }

    private SubmitResult Route(PlayerId player, PlayerAction action)
    {
        switch (action)
        {
            case RequestUndo:
                return _core.RequestUndo(player);
            case AnswerUndo answer:
                if (_core.UndoRequestedBy is not { } requester || MatchCore.Opponent(requester) != player)
                    return SubmitResult.Reject(RejectionCode.UnexpectedAction, "There is no undo request for you to answer.");
                if (!answer.Accept)
                {
                    _core.UndoRequestedBy = null;
                    return new SubmitResult(true, null, []);
                }
                _core = MatchCore.Replay(_core.Setup, _core.Db, _core.Log.Take(_core.UndoIndex(requester)));
                return new SubmitResult(true, null, []);
            default:
                return _core.Submit(player, action);
        }
    }

    /// <summary>What to save: the versions, the setup and the log (spec §6.5).</summary>
    public MatchRecord ToRecord() => new(EngineInfo.Version, _core.Db.Fingerprint, _core.Setup, [.. _core.Log]);

    /// <summary>Rebuilds a saved match by replaying it. Refuses records from another engine build or other card data (spec §6.7).</summary>
    public static Match Load(MatchRecord record, CardDatabase db)
    {
        if (record.EngineVersion != EngineInfo.Version || record.DataFingerprint != db.Fingerprint)
            throw new MatchVersionMismatchException(record.EngineVersion, EngineInfo.Version, record.DataFingerprint, db.Fingerprint);
        return new Match(MatchCore.Replay(record.Setup, db, record.Log));
    }
}
