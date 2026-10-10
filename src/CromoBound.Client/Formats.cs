using CromoBound.Engine.Matches;

namespace CromoBound.Client;

/// <summary>How formats and match stages read in the app.</summary>
public static class Formats
{
    public static string Name(MatchFormat format) => format == MatchFormat.Bo3 ? "Best of three" : "Best of one";

    /// <summary>For the middle of a sentence: "a best of three".</summary>
    public static string InSentence(MatchFormat format) => format == MatchFormat.Bo3 ? "a best of three" : "a best of one";

    public static string Stage(MatchStage stage) => stage switch
    {
        MatchStage.PickBattlefields => "Picking battlefields",
        MatchStage.PlayOrder => "Choosing who goes first",
        MatchStage.Sideboarding => "Sideboarding",
        MatchStage.Mulligan => "Mulligan",
        MatchStage.Playing => "Playing",
        _ => "Over",
    };
}
