using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class MatchJsonTests
{
    [Fact]
    public void A_match_record_round_trips_with_every_kind_of_action()
    {
        var deck = new Deck { Name = "d", Legend = "l", Champion = "c", Main = [new DeckEntry { Printing = "p", Count = 3 }] };
        var p1 = new PlayerId(0);
        PlayerAction[] actions =
        [
            new PickBattlefield("bf"),
            new ChoosePlayOrder(true),
            new SubmitSideboard { Swaps = [new SideboardSwap("a", "b")], Champion = "c2" },
            new Mulligan { SetAside = [new ObjectId(3)] },
            new Concede(),
            new ManualMoveCard(new ObjectId(5), Place.Trash(p1)),
            new ManualSetStatus(new ObjectId(5), StatusKind.Stunned, true),
            new ManualModifyMight(new ObjectId(5), 2, Duration.ThisTurn),
            new ManualAdjustPool(p1, 1, Domain.Fury, 1),
            new ManualLookAtTop(p1, PlaceKind.MainDeck, 3),
            new AddAbilityToChain(new ObjectId(5), 1, AbilityKind.Triggered) { Cost = new TotalCost(1, []) },
        ];
        var record = new MatchRecord("1.0.0+abc", "fp", new MatchSetup(MatchFormat.Bo3, deck, deck, 42),
            [.. actions.Select(a => new LoggedAction(p1, a))]);

        var json = CromoJson.Serialize(record);
        var back = CromoJson.Deserialize<MatchRecord>(json);

        Assert.Equal(json, CromoJson.Serialize(back));
        var ability = Assert.IsType<AddAbilityToChain>(back.Log[^1].Action);
        Assert.NotNull(ability.Cost!.Power);
        Assert.Equal(42UL, back.Setup.Seed);
    }
}
