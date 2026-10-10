using CromoBound.Data;
using CromoBound.Engine.Effects;

namespace CromoBound.Engine.Tests;

/// <summary>The load-time check of spec §7, as a data test: every effects file in data/effects runs with the status it declares.</summary>
public class EffectsDataTests
{
    private static readonly CardDatabase Data = CardRepository.Load(RepoPaths.Data);
    private static readonly CardEffects Real = new(Data);

    [Fact]
    public void Every_mapped_card_runs_as_mapped()
    {
        Assert.NotEmpty(Data.Effects);
        Assert.All(Data.Effects, effects =>
        {
            var info = Real.For(effects.Key);
            Assert.True(info.Unsupported.Count == 0, $"{effects.Key}: {string.Join(", ", info.Unsupported)}");
            Assert.Equal(effects.Value.File.Status, info.Status);
        });
    }

    [Theory]
    [InlineData("harnessed-dragon")]
    [InlineData("punch-first")]
    [InlineData("divining-shells")]
    [InlineData("dorans-blade")]
    [InlineData("shepherds-heirloom")]
    [InlineData("rampage")]
    [InlineData("sacrifice")]
    [InlineData("kayle-justified")]
    [InlineData("fiora-victorious")]
    [InlineData("risen-altar")]
    [InlineData("sunken-temple")]
    [InlineData("amateur-recital")]
    [InlineData("rengar-trophy-hunter")]
    public void The_fiora_decks_plan_o_cards_run_in_full(string cardId)
    {
        var info = Real.For(cardId);

        Assert.True(info.Unsupported.Count == 0, string.Join(", ", info.Unsupported));
        Assert.Equal(Models.Effects.MappingStatus.Full, info.Status);
    }
}
