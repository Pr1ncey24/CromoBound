using CromoBound.Data;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

internal static class TestDb
{
    public static Card Card(string id, string rich = "", CardCost? cost = null, Supertype? supertype = null) =>
        new() { Id = id, Name = id, Type = CardType.Unit, Supertype = supertype, Cost = cost, Text = new CardText { Rich = rich } };

    public static CardDatabase With(IEnumerable<Card> cards, IEnumerable<Printing>? printings = null, params (string FileName, string Json)[] effects) => new()
    {
        Cards = cards.ToDictionary(c => c.Id),
        Printings = (printings ?? Array.Empty<Printing>()).ToDictionary(p => p.Id),
        Sets = new Dictionary<string, CardSet>(),
        Effects = effects
            .Select(e => new LoadedEffects(e.FileName, CromoJson.Deserialize<EffectsFile>(e.Json)))
            .ToDictionary(e => e.File.CardId),
    };
}
