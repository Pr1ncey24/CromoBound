using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Tests;
using CromoBound.Engine.Views;

namespace CromoBound.Server.Tests;

/// <summary>What travels on the hub and into saved records reads back exactly: empty lists stay empty lists, events keep their
/// numbers.</summary>
public class WireJsonTests
{
    [Fact]
    public void Every_view_of_a_scripted_game_reads_back_exactly()
    {
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1, 5), ServerFactory.TestCards).Match!;
        var bots = new[] { new Bot(), new Bot() };

        for (var i = 0; i < 400 && match.Stage != MatchStage.Over; i++)
        {
            foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
            {
                var json = JsonSerializer.Serialize(match.ViewFor(seat), WireJson.Options);
                Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<PlayerView>(json, WireJson.Options), WireJson.Options));
            }
            var player = match.Pending!.Players[0];
            Assert.True(match.Submit(player, bots[player.Index].Choose(match)).Accepted);
        }

        Assert.Equal(MatchStage.Over, match.Stage);
    }

    [Fact]
    public void Empty_lists_are_written_as_empty_lists()
    {
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1), ServerFactory.TestCards).Match!;

        var json = JsonSerializer.Serialize(match.ToRecord(), WireJson.Options);

        Assert.Contains("\"log\":[]", json);
    }

    [Fact]
    public void The_wire_is_not_indented()
    {
        var json = JsonSerializer.Serialize(new HubReply(Guid.Empty, "x"), WireJson.Options);

        Assert.DoesNotContain('\n', json);
    }
}
