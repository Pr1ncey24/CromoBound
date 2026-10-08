using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class ActionJsonTests
{
    [Fact]
    public void Actions_round_trip_with_a_type_discriminator()
    {
        PlayerAction[] actions =
        [
            new PlayCard(new ObjectId(4)),
            new StandardMove { Units = [new ObjectId(1), new ObjectId(2)], Destination = Place.Battlefield(1) },
            new PayCost { Exhaust = [new ObjectId(7)] },
            new Pass(),
            new ChoosePlayOptions(Place.Base(new PlayerId(0)), true),
            new AssignDamage { Assignments = [new DamageAssignment(new ObjectId(3), 2)] },
        ];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);
            var back = CromoJson.Deserialize<PlayerAction>(json);

            Assert.Contains("\"type\"", json);
            Assert.DoesNotContain("isBoard", json);
            Assert.Equal(json, CromoJson.Serialize(back));
        }
    }
}
