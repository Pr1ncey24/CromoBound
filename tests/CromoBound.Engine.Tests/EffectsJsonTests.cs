using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EffectsJsonTests
{
    [Fact]
    public void Effect_actions_round_trip_through_json()
    {
        PlayerAction[] actions =
        [
            new ChoosePlayer(P2),
            new ChooseCards { Cards = [new ObjectId(3), new ObjectId(5)] },
            new ChooseOptional(true),
            new OrderTriggers { Order = [1, 0] },
            new ActivateAbility(new ObjectId(7), 1),
        ];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);

            Assert.Contains($"\"{action.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        }
    }

    [Fact]
    public void Effect_decisions_round_trip_through_json()
    {
        PendingDecision[] decisions =
        [
            new PriorityDecision(P1, [], [], [], [], [new ActivateOption(new ObjectId(7), 1)], CanPass: false, CanEndTurn: true),
            new ChoosePlayerDecision(P1, "kharox", [P2]),
            new ChooseCardsDecision(P1, "kharox", [new ObjectId(3)], 1, 1),
            new OptionalDecision(P1, "kharox", "Do the rest of the ability?"),
            new OrderTriggersDecision(P1, [new TriggerOption(new ObjectId(4), "unit-3", null), new TriggerOption(new ObjectId(6), "unit-3", "text")]),
        ];

        foreach (var decision in decisions)
        {
            var json = CromoJson.Serialize(decision);

            Assert.Contains($"\"{decision.GetType().Name.Replace("Decision", "")}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PendingDecision>(json)));
        }
    }

    [Fact]
    public void Effect_events_round_trip_through_json()
    {
        GameEvent[] events =
        [
            new CardPlayed(new ObjectId(4), "unit-2", P1),
            new AbilityActivated(new ObjectId(7), 1, P1),
            new TriggerAdded(3, new ObjectId(4), P2),
            new PlayerChosen(P1, P2),
            new Predicted(P1, 1, ["unit-2"]) { VisibleTo = P1 },
            new Predicted(P1, 1, null),
        ];

        foreach (var gameEvent in events)
        {
            var json = CromoJson.Serialize(gameEvent);

            Assert.Contains($"\"{gameEvent.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<GameEvent>(json)));
        }
    }

    [Theory]
    [InlineData("""{ "type": "ChooseCards", "cards": null }""")]
    [InlineData("""{ "type": "OrderTriggers", "order": null }""")]
    public void A_null_list_in_an_effect_answer_is_rejected(string json)
    {
        var engine = new TestGame().Start();

        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, CromoJson.Deserialize<PlayerAction>(json)).Rejection!.Code);
    }

    [Fact]
    public void Priority_lists_no_activations_yet()
    {
        var engine = new TestGame().Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }
}
