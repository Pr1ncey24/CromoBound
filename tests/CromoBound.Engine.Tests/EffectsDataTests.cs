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
}
