using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>A Bo1 or Bo3 match between two players (spec §4). The server's entry point to the engine.</summary>
public sealed partial class Match
{
    private MatchCore _core;

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

    public SubmitResult Submit(PlayerId player, PlayerAction action) => _core.Submit(player, action);
}
