using CromoBound.Client.Board;
using CromoBound.Contracts;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

/// <summary>A board built in code: a small catalog (printing <c>p-{cardId}</c> for each card) and the zones of a view, filled by the
/// test, then turned into the viewer's <see cref="PlayerView"/>. The viewer is seat 0 ("marco"), the opponent seat 1 ("giulia").</summary>
internal sealed class TestBoard
{
    public static readonly PlayerId Me = new(0);
    public static readonly PlayerId Them = new(1);

    private static CatalogCard C(string id, string name, CardType type, int? energy = null, int? might = null, Supertype? supertype = null) =>
        new(id, name, type, supertype, [], energy, [], might, supertype == Supertype.Token ? null : $"p-{id}");

    public static CardCatalog Catalog { get; } = new(
        [
            C("jinx-legend", "Jinx, Loose Cannon", CardType.Legend),
            C("jinx-champ", "Jinx, Rebel", CardType.Unit, 3, 3, Supertype.Champion),
            C("unit-a", "Blade Twirler", CardType.Unit, 2, 2),
            C("unit-b", "Daring Poro", CardType.Unit, 1, 1),
            C("gear-a", "Doran's Blade", CardType.Gear, 1),
            C("spell-a", "Angle Shot", CardType.Spell, 2),
            C("fury-rune", "Fury Rune", CardType.Rune, supertype: Supertype.Basic),
            C("order-rune", "Order Rune", CardType.Rune, supertype: Supertype.Basic),
            C("bf-a", "Back-Alley Bar", CardType.Battlefield),
            C("bf-b", "Altar to Unity", CardType.Battlefield),
            C("bf-c", "Dusk Rose Lab", CardType.Battlefield),
            C("token-bird", "Bird", CardType.Unit, might: 1, supertype: Supertype.Token),
        ],
        [.. new[] { "jinx-legend", "jinx-champ", "unit-a", "unit-b", "gear-a", "spell-a", "fury-rune", "order-rune", "bf-a", "bf-b", "bf-c" }
            .Select(id => new CatalogPrinting($"p-{id}", id, id.StartsWith("bf-", StringComparison.Ordinal) ? Orientation.Landscape : Orientation.Portrait))]);

    public CardBook Book { get; } = new(Catalog);

    private int _ids;

    public TestBoard()
    {
        MyLegend.Add(Card("jinx-legend", Me));
        TheirLegend.Add(Card("jinx-legend", Them));
        MyChampion.Add(Card("jinx-champ", Me, might: 3));
        TheirChampion.Add(Card("jinx-champ", Them, might: 3));
    }

    public List<CardView> MyLegend { get; } = [];
    public List<CardView> TheirLegend { get; } = [];
    public List<CardView> MyChampion { get; } = [];
    public List<CardView> TheirChampion { get; } = [];
    public List<CardView> Hand { get; } = [];
    public List<CardView> MyBase { get; } = [];
    public List<CardView> TheirBase { get; } = [];
    public List<CardView> MyTrash { get; } = [];
    public List<CardView> MyBanished { get; } = [];
    public List<ChainItemView> Chain { get; } = [];
    public List<GameEvent> Log { get; } = [];
    public List<(CardView Card, PlayerId? Controller, List<CardView> Units)> Lanes { get; } = [];

    public int TheirHandCount { get; set; } = 4;
    public int MyPoints { get; set; } = 5;
    public int TheirPoints { get; set; } = 6;
    public int MyXp { get; set; } = 1;
    public int MyWins { get; set; } = 1;
    public int TheirWins { get; set; }
    public PlayerId TurnPlayer { get; set; } = Me;
    public int TurnNumber { get; set; } = 5;
    public Phase Phase { get; set; } = Phase.Main;
    public MatchStage Stage { get; set; } = MatchStage.Playing;
    public MatchFormat Format { get; set; } = MatchFormat.Bo3;
    public int GameNumber { get; set; } = 2;

    public CardView Card(string cardId, PlayerId owner, bool exhausted = false, int damage = 0, int? might = null, ObjectId? attachedTo = null,
        bool stunned = false, bool buffed = false) =>
        new(new ObjectId(++_ids), cardId, Catalog.Cards.First(c => c.Id == cardId).DefaultPrintingId, owner, owner,
            exhausted, stunned, buffed, false, damage, might, null, MappingStatus.Full, [], attachedTo);

    /// <summary>Adds a card to a zone list and returns it, so a test can name it in decisions.</summary>
    public CardView Add(List<CardView> zone, string cardId, PlayerId? owner = null, bool exhausted = false, int damage = 0, int? might = null,
        ObjectId? attachedTo = null)
    {
        var card = Card(cardId, owner ?? (ReferenceEquals(zone, TheirBase) ? Them : Me), exhausted, damage, might, attachedTo);
        zone.Add(card);
        return card;
    }

    public CardView AddLane(string cardId, PlayerId? controller = null)
    {
        var card = Card(cardId, Me);
        Lanes.Add((card, controller, []));
        return card;
    }

    public CardView AddToLane(int lane, string cardId, PlayerId owner, bool exhausted = false, int? might = null)
    {
        var card = Card(cardId, owner, exhausted, might: might);
        Lanes[lane].Units.Add(card);
        return card;
    }

    public PlayerView View(PendingDecision? decision = null, IReadOnlyList<PlayerId>? deciding = null, string? kind = null)
    {
        PlayerSideView Side(PlayerId player) => player == Me
            ? new PlayerSideView(Me, MyPoints, MyXp, MyWins, new PoolView(2, new Dictionary<Domain, int> { [Domain.Fury] = 1 }, 0),
                MyLegend, MyChampion, MyBase, Hand, Hand.Count, 29, 7, MyTrash, MyBanished, null, 0)
            : new PlayerSideView(Them, TheirPoints, 2, TheirWins, new PoolView(0, new Dictionary<Domain, int>(), 0),
                TheirLegend, TheirChampion, TheirBase, null, TheirHandCount, 27, 8, [], [], null, 0);
        var turn = Stage == MatchStage.Playing
            ? new TurnView(TurnNumber, TurnPlayer, Phase, TurnStep.None, TurnPlayer, null, Chain.Count > 0, null, false, null, null, [[], []])
            : null;
        var lanes = Lanes.Select((l, i) => new BattlefieldView(i, l.Card, l.Controller, null, l.Units, false, null)).ToList();
        return new PlayerView(Me, Format, Stage, GameNumber, null, [Side(Me), Side(Them)], turn, lanes, Chain,
            deciding ?? (decision is null ? [] : decision.Players), kind ?? decision?.GetType().Name.Replace("Decision", ""), decision, Log);
    }

    public BoardModel Model(PendingDecision? decision = null, Interaction? interaction = null, IReadOnlyList<PlayerId>? deciding = null,
        string? kind = null) =>
        BoardModel.From(View(decision, deciding, kind), Book, "marco", "giulia", interaction ?? Idle.Instance);

    public static PriorityDecision Priority(
        IEnumerable<ObjectId>? playable = null, IEnumerable<RuneOption>? runes = null, IEnumerable<MoveOption>? moves = null,
        IEnumerable<HideOption>? hides = null, IEnumerable<ActivateOption>? activations = null, bool canPass = false, bool canEndTurn = true) =>
        new(Me, [.. playable ?? []], [.. runes ?? []], [.. moves ?? []], [.. hides ?? []], [.. activations ?? []], canPass, canEndTurn);
}
