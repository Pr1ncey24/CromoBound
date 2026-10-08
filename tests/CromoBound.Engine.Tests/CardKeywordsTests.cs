using CromoBound.Engine.Rules;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

public class CardKeywordsTests
{
    private static IReadOnlySet<DisplayKeyword> Own(string rich) =>
        CardKeywords.Own(new Card { Id = "x", Name = "x", Type = CardType.Unit, Text = new CardText { Rich = rich } });

    [Theory]
    [InlineData("<p>[Tank] (I must be assigned combat damage first.)</p>", new[] { DisplayKeyword.Tank })]
    [InlineData("<p>[Accelerate] (You may pay more.)<br />[Ganking]</p>", new[] { DisplayKeyword.Accelerate, DisplayKeyword.Ganking })]
    [InlineData("<p>[Shield 2] [Tank]</p>", new[] { DisplayKeyword.Shield, DisplayKeyword.Tank })]
    [InlineData("<p>[Quick-Draw]</p>", new[] { DisplayKeyword.QuickDraw })]
    [InlineData("<p>[Reaction] (Play any time.)<br />Deal 2 to a unit.</p>", new[] { DisplayKeyword.Reaction })]
    public void Keywords_starting_a_line_are_the_cards_own(string rich, DisplayKeyword[] expected) =>
        Assert.Equal(expected.Order(), Own(rich).Order());

    [Theory]
    [InlineData("<p>[Reaction][&gt;] :rb_exhaust:: [Add] :rb_energy_1:.</p>")]
    [InlineData("<p>:rb_exhaust:: [Reaction] - Pay any amount of Energy.</p>")]
    [InlineData("<p>Give a unit [Shield 3] and [Tank] this turn.</p>")]
    [InlineData("<p>While I'm buffed, I have [Ganking].</p>")]
    [InlineData("<p>Units here with [Temporary] have [Shield].</p>")]
    [InlineData("<p>[Add] :rb_energy_1:.</p>")]
    public void Granted_conditional_and_ability_keywords_are_not_the_cards_own(string rich) =>
        Assert.Empty(Own(rich));
}
