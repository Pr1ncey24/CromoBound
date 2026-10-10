using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Board;

/// <summary>Everything the board draws, from the player's view, the catalog and what the player is in the middle of (spec §4). What
/// every click does is worked out here, once, so the components only draw and report clicks.</summary>
public sealed partial class BoardModel
{
    private readonly Dictionary<ObjectId, BoardStep> _cardSteps = [];
    private readonly Dictionary<int, BoardStep> _laneSteps = [];
    private BoardStep _baseStep = NoStep.Instance;
    private BoardStep _cancel = NoStep.Instance;

    private BoardModel() { }

    public BoardSide Me { get; private set; } = default!;
    public BoardSide Them { get; private set; } = default!;
    public IReadOnlyList<BoardLane> Lanes { get; private set; } = [];
    public IReadOnlyList<ChainRow> Chain { get; private set; } = [];

    /// <summary>The turn number, 0 before the first turn.</summary>
    public int Turn { get; private set; }

    public Phase? Phase { get; private set; }

    /// <summary>"YOUR TURN" or "GIULIA'S TURN"; empty before the game has a turn.</summary>
    public string Badge { get; private set; } = "";

    public string ScoreLine { get; private set; } = "";
    public BoardButton Button { get; private set; } = default!;
    public IReadOnlyList<BoardButton> Extras { get; private set; } = [];
    public string? Hint { get; private set; }
    public BoardPanel? Panel { get; private set; }

    /// <summary>The player asked for an undo and the opponent hasn't answered yet.</summary>
    public bool UndoWaiting { get; private set; }

    public bool CanRequestUndo { get; private set; }
    public IReadOnlyList<string> Log { get; private set; } = [];

    /// <summary>The engine is waiting for this player.</summary>
    public bool MyDecision { get; private set; }

    public BoardStep Click(ObjectId card) => _cardSteps.GetValueOrDefault(card, NoStep.Instance);

    public BoardStep ClickLane(int battlefield) => _laneSteps.GetValueOrDefault(battlefield, NoStep.Instance);

    public BoardStep ClickBase() => _baseStep;

    /// <summary>Esc: drops a half-made move.</summary>
    public BoardStep Cancel() => _cancel;

    public static BoardModel From(PlayerView view, CardBook book, string me, string opponent, Interaction interaction)
    {
        var model = new BoardModel();
        var build = new Build(view, book, me, opponent);
        model.MyDecision = view.Decision is not null;
        model.Turn = view.Turn?.Number ?? 0;
        model.Phase = view.Turn?.Phase;
        model.Badge = view.Turn is null ? "" : view.Turn.TurnPlayer == view.Viewer ? "YOUR TURN" : $"{opponent.ToUpperInvariant()}'S TURN";
        model.ScoreLine = ScoreLineOf(view, opponent);
        model.Chain = [.. view.Chain.Reverse().Select(i => new ChainRow(
            i.Id, book.NameOf(i.Card?.CardId ?? i.SourceCardId ?? ""), i.Controller == view.Viewer ? me : opponent))];
        model.Button = new BoardButton("END TURN", view.Decision is null && view.Deciding.Count > 0 ? $"Waiting for {opponent}" : null, NoStep.Instance);
        model.Decide(view, build, interaction);
        model.Log = ActivityLog.Lines(view, book, me, opponent);
        model.CanRequestUndo = view.Stage == MatchStage.Playing && !model.UndoWaiting && view.Decision is not ConfirmUndoDecision && view.Log.Count > 0;
        model.Me = build.Side(view.Viewer, me, model);
        model.Them = build.Side(new PlayerId(1 - view.Viewer.Index), opponent, model);
        model.Lanes = [.. view.Battlefields.Select(b => build.Lane(b, model))];
        return model;
    }

    /// <summary>Fills the clicks, marks, button and panel for the player's decision. Tasks 2 and 3 fill this in; a decision without its
    /// own handling changes nothing.</summary>
    private partial void Decide(PlayerView view, Build build, Interaction interaction);

    private static string ScoreLineOf(PlayerView view, string opponent)
    {
        var mine = view.Players[view.Viewer.Index].GameWins;
        var theirs = view.Players[1 - view.Viewer.Index].GameWins;
        var score = mine > theirs ? $"you lead {mine} : {theirs}" : theirs > mine ? $"{opponent} leads {theirs} : {mine}" : $"{mine} : {theirs}";
        return $"{Formats.Name(view.Format)} · game {view.GameNumber} · {score}";
    }

    // The marks the decision handling sets, read when the cards are built.
    private readonly Dictionary<ObjectId, Ring> _rings = [];
    private readonly Dictionary<ObjectId, PayMark> _marks = [];
    private readonly HashSet<int> _destinations = [];
    private bool _baseIsDestination;

    /// <summary>Turns the view's cards into board cards, with the catalog's data and the decision's marks.</summary>
    private sealed class Build(PlayerView view, CardBook book, string me, string opponent)
    {
        private readonly Dictionary<ObjectId, int> _gear = AllCards(view).Where(c => c.AttachedTo is not null)
            .GroupBy(c => c.AttachedTo!.Value).ToDictionary(g => g.Key, g => g.Count());

        public PlayerView View => view;
        public CardBook Book => book;
        public string MeName => me;
        public string Opponent => opponent;

        public BoardCard Card(CardView card, BoardModel model)
        {
            var info = book.Card(card.CardId);
            return new BoardCard(
                card.Id, card.CardId, card.PrintingId ?? info?.DefaultPrintingId, info?.Name ?? CardBook.Unknown, info?.Type, info?.Energy,
                info?.Might, card.Exhausted, card.Stunned, card.Buffed, card.Empowered, card.Damage, card.Might, _gear.GetValueOrDefault(card.Id),
                model._cardSteps.ContainsKey(card.Id), model._rings.GetValueOrDefault(card.Id), model._marks.GetValueOrDefault(card.Id),
                card.EmpowerCount);
        }

        public BoardSide Side(PlayerId player, string name, BoardModel model)
        {
            var side = view.Players[player.Index];
            var onBase = side.Base.Where(c => c.AttachedTo is null).Select(c => Card(c, model)).ToList();
            return new BoardSide(
                player, name, player == view.Viewer, side.Points, side.Xp, side.Pool,
                side.Legend.Select(c => Card(c, model)).FirstOrDefault(), side.ChampionZone.Select(c => Card(c, model)).FirstOrDefault(),
                [.. onBase.Where(c => c.Type != CardType.Rune)], [.. onBase.Where(c => c.Type == CardType.Rune)],
                [.. (side.Hand ?? []).Select(c => Card(c, model))], side.HandCount, side.MainDeckCount, side.RuneDeckCount,
                side.Trash.Count > 0 ? Card(side.Trash[^1], model) : null, side.Trash.Count,
                [.. side.Banishment.Select(c => Card(c, model))], player == view.Viewer && model._baseIsDestination);
        }

        public BoardLane Lane(BattlefieldView lane, BoardModel model) => new(
            lane.Index, Card(lane.Card, model), lane.Controller, lane.ContestedBy is not null,
            [.. lane.Units.Where(u => u.AttachedTo is null && u.Controller == view.Viewer).Select(u => Card(u, model))],
            [.. lane.Units.Where(u => u.AttachedTo is null && u.Controller != view.Viewer).Select(u => Card(u, model))],
            lane.HasFacedown, lane.Facedown is { } hidden ? Card(hidden, model) : null, model._destinations.Contains(lane.Index));

        public CardView? Find(ObjectId id) => AllCards(view).FirstOrDefault(c => c.Id == id);

        private static IEnumerable<CardView> AllCards(PlayerView view) =>
            view.Players.SelectMany(p => p.Legend.Concat(p.ChampionZone).Concat(p.Base).Concat(p.Hand ?? []).Concat(p.Trash).Concat(p.Banishment))
                .Concat(view.Battlefields.SelectMany(b => b.Units.Append(b.Card).Concat(b.Facedown is { } f ? [f] : [])));
    }
}
