using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

/// <summary>A small, hand-made card pool for rules tests. Printing ids are "p-" + card id.</summary>
internal static class EngineTestDb
{
    /// <summary>"<p>[Tank]<br />body</p>": keywords on their own lines first, as on real cards.</summary>
    private static string RichWith(string body, DisplayKeyword[]? keywords) =>
        $"<p>{string.Concat((keywords ?? []).Select(k => $"[{k}]<br />"))}{body}</p>";

    private static Card Unit(string id, Domain domain, int energy, int might, DisplayKeyword[]? keywords = null, int power = 0) => new()
    {
        Id = id, Name = id, Type = CardType.Unit, Domains = [domain], Might = might, Keywords = keywords ?? [],
        Cost = new CardCost { Energy = energy, Power = [.. Enumerable.Repeat(Symbol(domain), power)] },
        Text = new CardText { Rich = RichWith(id, keywords) },
    };

    private static Card Spell(string id, DisplayKeyword[]? keywords = null) => new()
    {
        Id = id, Name = id, Type = CardType.Spell, Domains = [Domain.Fury], Keywords = keywords ?? [],
        Cost = new CardCost { Energy = 1 }, Text = new CardText { Rich = RichWith($"{id} text", keywords) },
    };

    private static Card Simple(string id, CardType type, Domain[] domains, Supertype? supertype = null, string[]? tags = null) => new()
    {
        Id = id, Name = id, Type = type, Domains = domains, Supertype = supertype, Tags = tags ?? [], Text = new CardText(),
    };

    private static Card Relic(string id, string rich) =>
        Simple(id, CardType.Gear, [Domain.Fury]) with { Cost = new CardCost { Energy = 1 }, Text = new CardText { Rich = rich } };

    private static PowerSymbol Symbol(Domain domain) => Enum.Parse<PowerSymbol>(domain.ToString());

    public static IReadOnlyList<Card> Cards { get; } =
    [
        Unit("unit-2", Domain.Fury, energy: 2, might: 2),
        Unit("unit-3", Domain.Chaos, energy: 3, might: 3, power: 1),
        Unit("tank-2", Domain.Fury, energy: 2, might: 2, [DisplayKeyword.Tank]),
        Unit("backline-1", Domain.Fury, energy: 1, might: 1, [DisplayKeyword.Backline]),
        Unit("ganker-2", Domain.Chaos, energy: 2, might: 2, [DisplayKeyword.Ganking]),
        Unit("accel-3", Domain.Fury, energy: 3, might: 3, [DisplayKeyword.Accelerate]),
        Unit("temp-1", Domain.Fury, energy: 1, might: 1, [DisplayKeyword.Temporary]) with
        {
            Text = new CardText { Rich = "<p>[Temporary] (At the start of its controller's Beginning phase, before scoring, kill this.)</p>" },
        },
        Unit("hidden-unit", Domain.Chaos, energy: 2, might: 2, [DisplayKeyword.Hidden]),
        Spell("spell"),
        Spell("action-spell", [DisplayKeyword.Action]),
        Spell("reaction-spell", [DisplayKeyword.Reaction]),
        Simple("gear-1", CardType.Gear, [Domain.Fury]) with { Cost = new CardCost { Energy = 1 } },
        Relic("dawn-relic", "<p>At the start of your Beginning phase, deal 1 to a unit.</p>"),
        Relic("each-relic", "<p>At the start of each player's Beginning phase, deal 1 to each unit.</p>"),
        Relic("noon-relic", "<p>At the start of your Main phase, draw 1.</p>"),
        Relic("dusk-relic", "<p>At the end of your turn, ready 2 runes.</p>"),
        Simple("fury-rune", CardType.Rune, [Domain.Fury], Supertype.Basic),
        Simple("chaos-rune", CardType.Rune, [Domain.Chaos], Supertype.Basic),
        Simple("bf-a", CardType.Battlefield, []),
        Simple("bf-b", CardType.Battlefield, []),
        Simple("jinx-legend", CardType.Legend, [Domain.Fury, Domain.Chaos], tags: ["Jinx"]),
        Unit("jinx-champ", Domain.Fury, energy: 3, might: 3) with { Supertype = Supertype.Champion, Tags = ["Jinx"] },
        Simple("token-recruit", CardType.Unit, [], Supertype.Token) with { Might = 1 },
        .. Enumerable.Range(1, 14).Select(i => Unit($"filler-{i}", i % 2 == 0 ? Domain.Fury : Domain.Chaos, energy: 1, might: 1)),
        Simple("bf-c", CardType.Battlefield, []),
        Simple("bf-d", CardType.Battlefield, []),
        Simple("bf-e", CardType.Battlefield, []),
        Simple("bf-f", CardType.Battlefield, []),
        Unit("jinx-alt", Domain.Chaos, energy: 2, might: 2) with { Supertype = Supertype.Champion, Tags = ["Jinx"] },
    ];

    public static CardDatabase Create() => new()
    {
        Cards = Cards.ToDictionary(c => c.Id, StringComparer.Ordinal),
        Printings = Cards.Where(c => c.Supertype != Supertype.Token)
            .Select(c => new Printing { Id = $"p-{c.Id}", CardId = c.Id, Set = "TST" })
            .ToDictionary(p => p.Id, StringComparer.Ordinal),
        Sets = new Dictionary<string, CardSet>(),
        Effects = new Dictionary<string, LoadedEffects>(),
    };
}

/// <summary>Legal decks built from <see cref="EngineTestDb"/>.</summary>
internal static class TestDecks
{
    /// <summary>Jinx legend and champion, filler-1..13 × 3, 6 + 6 runes, the given battlefields, filler-14 × 3 in the sideboard.</summary>
    public static Deck Jinx(params string[] battlefields) => new()
    {
        Name = "Jinx",
        Legend = "p-jinx-legend",
        Champion = "p-jinx-champ",
        Main = [.. Enumerable.Range(1, 13).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 })],
        Runes = [new DeckEntry { Printing = "p-fury-rune", Count = 6 }, new DeckEntry { Printing = "p-chaos-rune", Count = 6 }],
        Battlefields = [.. battlefields.Select(b => $"p-{b}")],
        Sideboard = [new DeckEntry { Printing = "p-filler-14", Count = 3 }],
    };

    public static MatchSetup Setup(MatchFormat format, ulong seed = 7) =>
        new(format, Jinx("bf-a", "bf-b", "bf-c"), Jinx("bf-d", "bf-e", "bf-f"), seed);
}
