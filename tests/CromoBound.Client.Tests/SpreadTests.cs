using CromoBound.Client.Board;

namespace CromoBound.Client.Tests;

public class SpreadTests
{
    [Fact]
    public void Cards_that_fit_keep_their_gap()
    {
        Assert.Equal(new[] { 0.0, 84, 168 }, Spread.Lefts(3, card: 76, box: 384, gap: 8));
    }

    [Fact]
    public void Cards_that_dont_fit_overlap_evenly_and_end_at_the_box_edge()
    {
        var lefts = Spread.Lefts(12, card: 44, box: 306, gap: 6);

        Assert.Equal(0, lefts[0]);
        Assert.Equal(306 - 44, lefts[^1], 6);
        Assert.All(lefts.Zip(lefts.Skip(1)), pair => Assert.Equal(lefts[1] - lefts[0], pair.Second - pair.First, 6));
    }

    [Fact]
    public void A_centered_row_that_fits_sits_in_the_middle()
    {
        Assert.Equal(new[] { 99.0, 185 }, Spread.Lefts(2, card: 76, box: 360, gap: 10, center: true));
    }

    [Fact]
    public void A_centered_row_that_doesnt_fit_fills_the_box()
    {
        var lefts = Spread.Lefts(6, card: 76, box: 344, gap: 10, center: true);

        Assert.Equal(0, lefts[0]);
        Assert.Equal(344 - 76, lefts[^1], 6);
    }

    [Fact]
    public void One_card_and_no_cards()
    {
        Assert.Equal(new[] { 0.0 }, Spread.Lefts(1, card: 76, box: 384, gap: 8));
        Assert.Equal(new[] { 142.0 }, Spread.Lefts(1, card: 76, box: 360, gap: 10, center: true));
        Assert.Empty(Spread.Lefts(0, card: 76, box: 384, gap: 8));
    }

    [Fact]
    public void A_box_narrower_than_one_card_still_puts_every_card_at_the_start()
    {
        Assert.Equal(new[] { 0.0, 0, 0 }, Spread.Lefts(3, card: 76, box: 50, gap: 8));
    }
}
