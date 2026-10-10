using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests;

/// <summary>Player views with only what the match page reads: format, stage, game number and game wins.</summary>
internal static class Views
{
    public static PlayerView Of(
        MatchFormat format = MatchFormat.Bo3, MatchStage stage = MatchStage.Playing, int game = 1, int myWins = 0, int theirWins = 0, int seat = 0)
    {
        PlayerSideView Side(int index, int wins) => new(
            new PlayerId(index), 0, 0, wins, new PoolView(0, new Dictionary<Domain, int>(), 0), [], [], [], null, 0, 0, 0, [], [], null, 0);
        PlayerSideView[] sides = seat == 0 ? [Side(0, myWins), Side(1, theirWins)] : [Side(0, theirWins), Side(1, myWins)];
        return new PlayerView(new PlayerId(seat), format, stage, game, null, sides, null, [], [], [], null, null, []);
    }
}
