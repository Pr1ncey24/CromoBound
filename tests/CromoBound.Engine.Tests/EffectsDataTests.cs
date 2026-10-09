using CromoBound.Data;
using CromoBound.Engine.Effects;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

/// <summary>Which real cards (data/effects) the engine runs today. Plans E and F move cards from the second list to the first.</summary>
public class EffectsDataTests
{
    private static readonly CardEffects Real = new(CardRepository.Load(RepoPaths.Data));

    [Theory]
    [InlineData("progress-day")]
    [InlineData("falling-star")]
    [InlineData("vengeance")]
    [InlineData("vanguard-sergeant")]
    [InlineData("horns-of-the-dragon")]
    [InlineData("token-sprite")]
    [InlineData("shadow-temple")]
    [InlineData("soaring-scout")]
    [InlineData("mystic-poro")]
    [InlineData("voracious-gromp")]
    [InlineData("kharox")]
    [InlineData("garbage-grabber")]
    [InlineData("fury-rune")]
    [InlineData("daring-poro")]
    [InlineData("mutated-mouser")]
    [InlineData("jeweled-colossus")]
    [InlineData("noxus-hopeful")]
    [InlineData("navori-scout")]
    [InlineData("pouty-poro")]
    [InlineData("token-bird")]
    [InlineData("soulspinner")]
    [InlineData("inferna")]
    [InlineData("rengar-trophy-hunter")]
    [InlineData("rengar-unseen")]
    public void Cards_the_engine_runs_today_are_full(string cardId) => Assert.Equal(MappingStatus.Full, Real.For(cardId).Status);

    [Theory]
    [InlineData("veteran-poro", "keyword Weaponmaster")]
    public void Cards_needing_later_plans_play_by_hand_for_now(string cardId, string missing)
    {
        var info = Real.For(cardId);

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(info.Unsupported, p => p.Contains(missing, StringComparison.Ordinal));
    }
}
