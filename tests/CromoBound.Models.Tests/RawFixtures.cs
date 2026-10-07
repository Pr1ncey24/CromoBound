using CromoBound.Importer;

namespace CromoBound.Models.Tests;

internal static class RawFixtures
{
    public static RawCard Card(
        string id, string name, string set = "OGN", string type = "Unit", string? supertype = null, string rarity = "Common",
        string[]? domains = null, int? energy = 2, int? might = 2, int? power = null, string rich = "", int? number = 1,
        bool altArt = false, bool overnumbered = false, bool signature = false) => new()
    {
        Id = id,
        Name = name,
        CollectorNumber = number,
        Attributes = new RawAttributes { Energy = energy, Might = might, Power = power },
        Classification = new RawClassification { Type = type, Supertype = supertype, Rarity = rarity, Domain = domains ?? new[] { "Fury" } },
        Text = new RawText { Rich = rich, Plain = rich },
        Set = new RawSetRef { SetId = set },
        Metadata = new RawMetadata { AlternateArt = altArt, Overnumbered = overnumbered, Signature = signature },
    };

    public static readonly RawSet[] Sets =
    [
        new() { SetId = "OGN", Name = "Origins", CardCount = 1, PublishedOn = "2025-10-31T00:00:00" },
        new() { SetId = "VEN", Name = "Vendetta", CardCount = 1, PublishedOn = "2026-07-31T00:00:00" },
    ];
}
