using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Random;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

/// <summary>Builds an exact game situation for a rules test, without going through setup.</summary>
internal sealed class TestGame
{
    public static readonly PlayerId P1 = new(0);
    public static readonly PlayerId P2 = new(1);

    /// <summary>Two battlefields, the first owned by P1 and the second by P2. A real battlefield card can replace a test one.</summary>
    public TestGame(ulong seed = 1, CardDatabase? db = null, string firstBattlefield = "bf-a", string secondBattlefield = "bf-b")
    {
        Db = db ?? EngineTestDb.Create();
        State = new GameState(2, new SeededRandom(seed));
        AddBattlefield(firstBattlefield, P1);
        AddBattlefield(secondBattlefield, P2);
    }

    public CardDatabase Db { get; }
    public GameState State { get; }

    /// <summary>The events returned by <see cref="Game.Start"/>.</summary>
    public IReadOnlyList<GameEvent> StartEvents { get; private set; } = [];

    /// <summary>Puts a card straight into a place. The owner defaults to the place's player, else P1.</summary>
    public ObjectId Put(string cardId, Place place, PlayerId? owner = null)
    {
        var isToken = Db.Cards[cardId].Supertype == Supertype.Token;
        return State.Create(cardId, isToken ? null : $"p-{cardId}", owner ?? place.Player ?? P1, place, isToken);
    }

    public void Runes(PlayerId player, string runeId, int count)
    {
        for (var i = 0; i < count; i++) Put(runeId, Place.Base(player));
    }

    /// <summary>The first object at a place with this card id.</summary>
    public ObjectId First(Place place, string cardId) => State.At(place).First(id => State[id].CardId == cardId);

    /// <summary>Gives each Main Deck <paramref name="filler"/> copies of unit-2 (so draws don't burn out) and starts with <paramref name="first"/>'s turn.</summary>
    public Game Start(PlayerId? first = null, int filler = 10)
    {
        foreach (var player in new[] { P1, P2 })
            for (var i = 0; i < filler; i++) Put("unit-2", Place.MainDeck(player));
        var game = new Game(State, Db);
        StartEvents = game.Start(first ?? P1);
        return game;
    }

    private void AddBattlefield(string cardId, PlayerId owner)
    {
        var index = State.Battlefields.Count;
        State.Battlefields.Add(new BattlefieldState(index, Put(cardId, Place.BattlefieldCard(index), owner)));
    }
}

internal static class GameTestExtensions
{
    /// <summary>Submits and asserts the action was accepted.</summary>
    public static SubmitResult Accept(this Game game, PlayerId player, PlayerAction action)
    {
        var result = game.Submit(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    public static T Decision<T>(this Game game) where T : PendingDecision => Assert.IsType<T>(game.Pending);

    public static SubmitResult PayWithSuggestion(this Game game, PlayerId player)
    {
        var suggestion = game.Decision<PayCostDecision>().Suggested;
        Assert.NotNull(suggestion);
        return game.Accept(player, new PayCost { Exhaust = suggestion.Exhaust, Recycle = suggestion.Recycle });
    }
}
