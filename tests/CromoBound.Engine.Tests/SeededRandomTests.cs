using CromoBound.Engine.Random;

namespace CromoBound.Engine.Tests;

public class SeededRandomTests
{
    [Fact]
    public void Seeding_matches_reference()
    {
        Assert.Equal(
            new RandomState(0xbdd732262feb6e95, 0x28efe333b266f103, 0x47526757130f9f52, 0x581ce1ff0e4ae394),
            new SeededRandom(42).State);
    }

    [Fact]
    public void Sequence_matches_reference()
    {
        var random = new SeededRandom(42);

        Assert.Equal(1546998764402558742UL, random.NextUInt64());
        Assert.Equal(6990951692964543102UL, random.NextUInt64());
        Assert.Equal(12544586762248559009UL, random.NextUInt64());
    }

    [Fact]
    public void D20_rolls_match_reference()
    {
        var random = new SeededRandom(42);

        Assert.Equal(new[] { 3, 3, 10, 14, 17 }, Enumerable.Range(0, 5).Select(_ => random.RollD20()).ToArray());
    }

    [Fact]
    public void Shuffle_matches_reference()
    {
        var items = Enumerable.Range(0, 10).ToList();

        new SeededRandom(7).Shuffle(items);

        Assert.Equal(new[] { 8, 3, 9, 0, 7, 2, 1, 6, 5, 4 }, items);
    }

    [Fact]
    public void Restored_state_continues_the_same_sequence()
    {
        var random = new SeededRandom(5);
        random.NextUInt64();

        var copy = new SeededRandom(random.State);

        Assert.Equal(random.NextUInt64(), copy.NextUInt64());
    }

    [Fact]
    public void NextInt_covers_the_whole_range_and_nothing_else()
    {
        var random = new SeededRandom(1);

        var values = Enumerable.Range(0, 1000).Select(_ => random.NextInt(6)).ToHashSet();

        Assert.Equal(Enumerable.Range(0, 6).ToHashSet(), values);
    }

    [Fact]
    public void NextInt_rejects_a_non_positive_bound() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).NextInt(0));
}
