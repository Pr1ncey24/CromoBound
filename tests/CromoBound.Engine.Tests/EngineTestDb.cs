using CromoBound.Data;
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

    private static PowerSymbol Symbol(Domain domain) => Enum.Parse<PowerSymbol>(domain.ToString());

    public static IReadOnlyList<Card> Cards { get; } =
    [
        Unit("unit-2", Domain.Fury, energy: 2, might: 2),
        Unit("unit-3", Domain.Chaos, energy: 3, might: 3, power: 1),
        Unit("tank-2", Domain.Fury, energy: 2, might: 2, [DisplayKeyword.Tank]),
        Unit("backline-1", Domain.Fury, energy: 1, might: 1, [DisplayKeyword.Backline]),
        Unit("ganker-2", Domain.Chaos, energy: 2, might: 2, [DisplayKeyword.Ganking]),
        Unit("accel-3", Domain.Fury, energy: 3, might: 3, [DisplayKeyword.Accelerate]),
        Unit("temp-1", Domain.Fury, energy: 1, might: 1, [DisplayKeyword.Temporary]),
        Unit("hidden-unit", Domain.Chaos, energy: 2, might: 2, [DisplayKeyword.Hidden]),
        Spell("spell"),
        Spell("action-spell", [DisplayKeyword.Action]),
        Spell("reaction-spell", [DisplayKeyword.Reaction]),
        Simple("gear-1", CardType.Gear, [Domain.Fury]) with { Cost = new CardCost { Energy = 1 } },
        Simple("fury-rune", CardType.Rune, [Domain.Fury], Supertype.Basic),
        Simple("chaos-rune", CardType.Rune, [Domain.Chaos], Supertype.Basic),
        Simple("bf-a", CardType.Battlefield, []),
        Simple("bf-b", CardType.Battlefield, []),
        Simple("jinx-legend", CardType.Legend, [Domain.Fury, Domain.Chaos], tags: ["Jinx"]),
        Unit("jinx-champ", Domain.Fury, energy: 3, might: 3) with { Supertype = Supertype.Champion, Tags = ["Jinx"] },
        Simple("token-recruit", CardType.Unit, [], Supertype.Token) with { Might = 1 },
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
