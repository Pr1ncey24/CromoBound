using CromoBound.Engine.Random;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.State;

/// <summary>All mutable state of one game. Only the engine changes it.</summary>
public sealed class GameState
{
    private readonly Dictionary<ObjectId, CardInstance> _objects = [];
    private readonly Dictionary<Place, List<ObjectId>> _zones = [];
    private int _nextObjectId = 1;
    private int _nextChainItemId = 1;

    public GameState(int playerCount, SeededRandom rng)
    {
        Players = [.. Enumerable.Range(0, playerCount).Select(i => new PlayerState(new PlayerId(i)))];
        Rng = rng;
    }

    public IReadOnlyList<PlayerState> Players { get; }
    public List<BattlefieldState> Battlefields { get; } = [];
    public TurnState Turn { get; } = new();
    public List<ChainItem> Chain { get; } = [];
    public SeededRandom Rng { get; }

    public CardInstance this[ObjectId id] => _objects[id];

    public bool Exists(ObjectId id) => _objects.ContainsKey(id);

    /// <summary>Every object, in id order (deterministic).</summary>
    public IEnumerable<CardInstance> Objects => _objects.Values.OrderBy(o => o.Id.Value);

    /// <summary>
    /// Objects at a place. For ordered places index 0 is the top. Returns a live view of the zone, so callers that
    /// move cards while walking it must copy it first (for example <c>[.. state.At(place)]</c>).
    /// </summary>
    public IReadOnlyList<ObjectId> At(Place place) => _zones.TryGetValue(place, out var list) ? list : [];

    public PlayerState Player(PlayerId id) => Players[id.Index];

    /// <summary>The other player (1v1).</summary>
    public PlayerId Opponent(PlayerId id) => new((id.Index + 1) % Players.Count);

    public int NextChainItemId() => _nextChainItemId++;

    /// <summary>Creates an object controlled by its owner, appended to the place (the bottom of an ordered pile).</summary>
    public ObjectId Create(string cardId, string? printingId, PlayerId owner, Place place, bool isToken = false)
    {
        place = OwnersPile(place, owner);
        var instance = new CardInstance(new ObjectId(_nextObjectId++), cardId, printingId, owner, isToken, place);
        _objects.Add(instance.Id, instance);
        Insert(place, instance.Id, DeckPosition.Bottom);
        return instance.Id;
    }

    /// <summary>
    /// Moves an object. Returns its id afterwards: a new id when it enters or leaves a non-board zone (CR 124),
    /// or null when a token leaves the board for anywhere but the chain and ceases to exist (CR 186.1).
    /// </summary>
    public ObjectId? Move(ObjectId id, Place to, DeckPosition position = DeckPosition.Top)
    {
        var instance = _objects[id];
        var from = instance.Place;
        to = OwnersPile(to, instance.Owner);
        _zones[from].Remove(id);

        if (instance.IsToken && !to.IsBoard && to.Kind != PlaceKind.Chain)
        {
            _objects.Remove(id);
            return null;
        }
        if (!from.IsBoard || !to.IsBoard)
        {
            _objects.Remove(id);
            instance.BecomeNewObject(new ObjectId(_nextObjectId++));
            _objects.Add(instance.Id, instance);
        }
        instance.Place = to;
        Insert(to, instance.Id, position);
        return instance.Id;
    }

    public void Shuffle(Place place)
    {
        if (_zones.TryGetValue(place, out var list)) Rng.Shuffle(list);
    }

    private static Place OwnersPile(Place place, PlayerId owner) => place.IsPlayerPile ? place with { Player = owner } : place;

    private void Insert(Place place, ObjectId id, DeckPosition position)
    {
        if (!_zones.TryGetValue(place, out var list)) _zones[place] = list = [];
        if (place.IsOrdered && position == DeckPosition.Top) list.Insert(0, id);
        else list.Add(id);
    }
}
