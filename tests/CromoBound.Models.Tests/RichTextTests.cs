using CromoBound.Models.Cards;

namespace CromoBound.Models.Tests;

public class RichTextTests
{
    [Fact]
    public void Lines_split_on_br_and_paragraphs()
    {
        Assert.Equal(new[] { "Deal 3 to a unit.", "Deal 3 to a unit." }, RichText.Lines("<p>Deal 3 to a unit.<br />Deal 3 to a unit.</p>"));
        Assert.Equal(new[] { "A", "B" }, RichText.Lines("<p>A</p><p>B</p>"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    public void Empty_text_has_no_lines(string? rich) => Assert.Empty(RichText.Lines(rich));

    [Fact]
    public void StripReminders_removes_parenthesized_text() =>
        Assert.Equal("[Vision]", RichText.StripReminders("[Vision] (When you play me, look at the top card.)"));
}
