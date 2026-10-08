using CromoBound.Data;
using CromoBound.Engine.Random;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

/// <summary>Builds an exact game situation for a rules test, without going through setup.</summary>
internal sealed class TestGame
{
    public static readonly PlayerId P1 = new(0);
    public static readonly PlayerId P2 = new(1);

    public TestGame(ulong seed = 1)
    {
        Db = EngineTestDb.Create();
        State = new GameState(2, new SeededRandom(seed));
        AddBattlefield("bf-a", P1);
        AddBattlefield("bf-b", P2);
    }

    public CardDatabase Db { get; }
    public GameState State { get; }

    /// <summary>Puts a card straight into a place. The owner defaults to the place's player, else P1.</summary>
    public ObjectId Put(string cardId, Place place, PlayerId? owner = null)
    {
        var isToken = Db.Cards[cardId].Supertype == Supertype.Token;
        return State.Create(cardId, isToken ? null : $"p-{cardId}", owner ?? place.Player ?? P1, place, isToken);
    }

    private void AddBattlefield(string cardId, PlayerId owner)
    {
        var index = State.Battlefields.Count;
        State.Battlefields.Add(new BattlefieldState(index, Put(cardId, Place.BattlefieldCard(index), owner)));
    }
}
