using System.Text.Json;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

public class CardJsonTests
{
    private const string FallingStar = """
        {
          "id": "falling-star",
          "name": "Falling Star",
          "type": "Spell",
          "domains": ["Fury"],
          "cost": { "energy": 2, "power": ["Fury", "Fury"] },
          "text": { "rich": "<p>Deal 3 to a unit.<br />Deal 3 to a unit.</p>", "plain": "Deal 3 to a unit. Deal 3 to a unit." },
          "defaultPrintingId": "69bc5bc8d308c64675ca86d4"
        }
        """;

    [Fact]
    public void Card_deserializes_all_fields()
    {
        var card = CromoJson.Deserialize<Card>(FallingStar);

        Assert.Equal("falling-star", card.Id);
        Assert.Equal(CardType.Spell, card.Type);
        Assert.Null(card.Supertype);
        Assert.Equal(new[] { Domain.Fury }, card.Domains);
        Assert.Equal(2, card.Cost!.Energy);
        Assert.Equal(new[] { PowerSymbol.Fury, PowerSymbol.Fury }, card.Cost.Power);
        Assert.Empty(card.Tags);
        Assert.Empty(card.Keywords);
        Assert.Equal("69bc5bc8d308c64675ca86d4", card.DefaultPrintingId);
    }

    [Fact]
    public void Serialization_omits_nulls_and_empty_lists_but_keeps_empty_power()
    {
        var card = new Card
        {
            Id = "x", Name = "X", Type = CardType.Unit,
            Cost = new CardCost { Energy = 2, Power = [] },
            Text = new CardText(),
        };

        var json = CromoJson.Serialize(card);

        Assert.DoesNotContain("supertype", json);
        Assert.DoesNotContain("tags", json);
        Assert.Contains("\"power\": []", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void Self_power_round_trips()
    {
        var card = new Card
        {
            Id = "x", Name = "X", Type = CardType.Spell, Domains = [Domain.Calm, Domain.Body],
            Cost = new CardCost { Energy = 3, Power = [PowerSymbol.Self] }, Text = new CardText(),
        };

        var back = CromoJson.Deserialize<Card>(CromoJson.Serialize(card));

        Assert.Equal(new[] { PowerSymbol.Self }, back.Cost!.Power);
    }

    [Fact]
    public void Unknown_property_is_rejected() =>
        Assert.Throws<JsonException>(() => CromoJson.Deserialize<Card>(FallingStar.Replace("\"name\":", "\"colour\": \"red\", \"name\":")));

    [Fact]
    public void Integer_enum_values_are_rejected() =>
        Assert.Throws<JsonException>(() => CromoJson.Deserialize<Card>(FallingStar.Replace("\"Spell\"", "1")));

    [Fact]
    public void Missing_required_property_is_rejected() =>
        Assert.Throws<JsonException>(() => CromoJson.Deserialize<Card>(FallingStar.Replace("\"id\": \"falling-star\",", "")));

    [Fact]
    public void Quick_draw_uses_hyphenated_name()
    {
        Assert.Equal("\"Quick-Draw\"", CromoJson.Serialize(DisplayKeyword.QuickDraw));
        Assert.Equal(DisplayKeyword.QuickDraw, CromoJson.Deserialize<DisplayKeyword>("\"Quick-Draw\""));
    }

    [Fact]
    public void Deck_round_trips()
    {
        const string json = """
            {
              "name": "Kharox Burn",
              "legend": "l1",
              "champion": "c1",
              "main": [ { "printing": "p1", "count": 2 }, { "printing": "p2", "count": 1 } ],
              "runes": [ { "printing": "r1", "count": 12 } ],
              "battlefields": ["b1", "b2", "b3"]
            }
            """;

        var deck = CromoJson.Deserialize<Deck>(json);

        Assert.Equal(2, deck.Main.Count);
        Assert.Equal("p2", deck.Main[1].Printing);
        Assert.Equal(3, deck.Battlefields.Count);
        Assert.Equal(CromoJson.Serialize(deck), CromoJson.Serialize(CromoJson.Deserialize<Deck>(CromoJson.Serialize(deck))));
    }

    [Fact]
    public void Deck_sideboard_round_trips_and_is_optional()
    {
        const string json = """
            {
              "name": "Jinx",
              "legend": "l1",
              "champion": "c1",
              "main": [ { "printing": "p1", "count": 3 } ],
              "sideboard": [ { "printing": "p2", "count": 2 } ]
            }
            """;

        var deck = CromoJson.Deserialize<Deck>(json);
        var withoutSideboard = deck with { Sideboard = [] };

        Assert.Equal("p2", Assert.Single(deck.Sideboard).Printing);
        Assert.Contains("\"sideboard\"", CromoJson.Serialize(deck));
        Assert.DoesNotContain("sideboard", CromoJson.Serialize(withoutSideboard));
    }

    [Fact]
    public void Printing_and_set_deserialize()
    {
        var printing = CromoJson.Deserialize<Printing>("""
            { "id": "p", "cardId": "vi", "set": "UNL", "collectorNumber": 229, "rarity": "Rare", "variant": "Signature", "orientation": "Portrait" }
            """);
        var set = CromoJson.Deserialize<CardSet>("""{ "id": "UNL", "name": "Unleashed", "publishedOn": "2026-05-08", "cardCount": 280 }""");

        Assert.Equal(PrintingVariant.Signature, printing.Variant);
        Assert.Equal(Rarity.Rare, printing.Rarity);
        Assert.Equal(new DateOnly(2026, 5, 8), set.PublishedOn);
    }
}
