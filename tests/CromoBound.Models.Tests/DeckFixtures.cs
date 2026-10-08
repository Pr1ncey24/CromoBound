using CromoBound.Data;
using CromoBound.Models.Cards;

namespace CromoBound.Models.Tests;

/// <summary>A small card pool and a legal Jinx deck. Printing ids are "p-" + card id.</summary>
internal static class DeckFixtures
{
    private static Card C(
        string id, CardType type, Domain[] domains, string[]? tags = null, Supertype? supertype = null, DisplayKeyword[]? keywords = null) => new()
    {
        Id = id, Name = id, Type = type, Supertype = supertype, Domains = domains, Tags = tags ?? [], Keywords = keywords ?? [], Text = new CardText(),
    };

    public static readonly IReadOnlyList<Card> Cards =
    [
        C("jinx-legend", CardType.Legend, [Domain.Fury, Domain.Chaos], ["Jinx"]),
        C("jinx-champ", CardType.Unit, [Domain.Fury], ["Jinx", "Zaun"], Supertype.Champion),
        C("vi-champ", CardType.Unit, [Domain.Body], ["Vi", "Piltover"], Supertype.Champion),
        .. Enumerable.Range(1, 14).Select(i => C($"unit-{i}", CardType.Unit, [i % 2 == 0 ? Domain.Fury : Domain.Chaos])),
        C("sig-1", CardType.Spell, [Domain.Fury], ["Jinx"], Supertype.Signature),
        C("sig-2", CardType.Spell, [Domain.Fury], ["Jinx"], Supertype.Signature),
        C("sig-3", CardType.Spell, [Domain.Chaos], ["Jinx"], Supertype.Signature),
        C("sig-4", CardType.Spell, [Domain.Chaos], ["Jinx"], Supertype.Signature),
        C("sig-vi", CardType.Spell, [Domain.Body], ["Vi"], Supertype.Signature),
        C("calm-unit", CardType.Unit, [Domain.Calm]),
        C("colorless-gear", CardType.Gear, [Domain.Colorless]),
        C("unique-gear", CardType.Gear, [Domain.Fury], keywords: [DisplayKeyword.Unique]),
        C("token-recruit", CardType.Unit, [], supertype: Supertype.Token),
        C("fury-rune", CardType.Rune, [Domain.Fury], supertype: Supertype.Basic),
        C("chaos-rune", CardType.Rune, [Domain.Chaos], supertype: Supertype.Basic),
        C("calm-rune", CardType.Rune, [Domain.Calm], supertype: Supertype.Basic),
        C("bf-1", CardType.Battlefield, []),
        C("bf-2", CardType.Battlefield, []),
        C("bf-3", CardType.Battlefield, []),
    ];

    public static CardDatabase Db { get; } =
        TestDb.With(Cards, Cards.Select(c => new Printing { Id = P(c.Id), CardId = c.Id, Set = "TST" }));

    public static string P(string cardId) => $"p-{cardId}";

    public static DeckEntry E(string cardId, int count) => new() { Printing = P(cardId), Count = count };

    /// <summary>Jinx legend and champion, 13 units × 3, 6 + 6 runes, 3 battlefields: exactly legal.</summary>
    public static Deck Legal() => new()
    {
        Name = "Legal",
        Legend = P("jinx-legend"),
        Champion = P("jinx-champ"),
        Main = [.. Enumerable.Range(1, 13).Select(i => E($"unit-{i}", 3))],
        Runes = [E("fury-rune", 6), E("chaos-rune", 6)],
        Battlefields = [P("bf-1"), P("bf-2"), P("bf-3")],
    };
}
