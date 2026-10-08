using CromoBound.Data;
using CromoBound.Models.Cards;
using static CromoBound.Models.Tests.DeckFixtures;

namespace CromoBound.Models.Tests;

public class DeckValidatorTests
{
    private static DeckReport Validate(Deck deck) => DeckValidator.Validate(deck, Db);

    private static DeckIssue Issue(DeckReport report, DeckIssueCode code) => Assert.Single(report.Issues, i => i.Code == code);

    [Fact]
    public void Legal_deck_has_no_issues()
    {
        var report = Validate(Legal());

        Assert.True(report.IsLegal);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Short_main_deck_is_incomplete()
    {
        var issue = Issue(Validate(Legal() with { Main = [.. Legal().Main.Skip(1)] }), DeckIssueCode.MainDeckSize);

        Assert.Equal(DeckIssueSeverity.Incomplete, issue.Severity);
        Assert.Equal(37, issue.Actual);
        Assert.Equal(40, issue.Expected);
    }

    [Fact]
    public void Oversized_main_deck_is_illegal()
    {
        var issue = Issue(Validate(Legal() with { Main = [.. Legal().Main, E("unit-14", 2)] }), DeckIssueCode.MainDeckSize);

        Assert.Equal(DeckIssueSeverity.Illegal, issue.Severity);
        Assert.Equal(42, issue.Actual);
    }

    [Fact]
    public void Rune_deck_needs_exactly_twelve()
    {
        var issue = Issue(Validate(Legal() with { Runes = [E("fury-rune", 6), E("chaos-rune", 5)] }), DeckIssueCode.RuneDeckSize);

        Assert.Equal(DeckIssueSeverity.Incomplete, issue.Severity);
        Assert.Equal(11, issue.Actual);
    }

    [Fact]
    public void Battlefields_need_three_with_different_names()
    {
        var duplicate = Validate(Legal() with { Battlefields = [P("bf-1"), P("bf-1"), P("bf-2")] });
        var missing = Validate(Legal() with { Battlefields = [P("bf-1"), P("bf-2")] });

        Assert.Equal(new[] { "bf-1" }, Issue(duplicate, DeckIssueCode.DuplicateBattlefield).CardIds);
        Assert.DoesNotContain(duplicate.Issues, i => i.Code == DeckIssueCode.BattlefieldCount);
        Assert.Equal(DeckIssueSeverity.Incomplete, Issue(missing, DeckIssueCode.BattlefieldCount).Severity);
    }

    [Fact]
    public void Sideboard_over_ten_is_illegal()
    {
        var report = Validate(Legal() with { Sideboard = [E("unit-14", 3), E("colorless-gear", 3), E("sig-1", 3), E("sig-2", 2)] });

        var issue = Assert.Single(report.Issues);
        Assert.Equal(DeckIssueCode.SideboardSize, issue.Code);
        Assert.Equal(11, issue.Actual);
    }

    [Fact]
    public void Copies_are_counted_across_main_and_sideboard()
    {
        var issue = Issue(Validate(Legal() with { Sideboard = [E("unit-1", 1)] }), DeckIssueCode.TooManyCopies);

        Assert.Equal(new[] { "unit-1" }, issue.CardIds);
        Assert.Equal(4, issue.Actual);
        Assert.Equal(3, issue.Expected);
    }

    [Fact]
    public void Champion_counts_toward_copies()
    {
        var report = Validate(Legal() with { Main = [.. Legal().Main.Skip(1), E("jinx-champ", 3)] });

        Assert.Equal(new[] { "jinx-champ" }, Issue(report, DeckIssueCode.TooManyCopies).CardIds);
        Assert.DoesNotContain(report.Issues, i => i.Code == DeckIssueCode.MainDeckSize);
    }

    [Fact]
    public void Unique_cards_are_limited_to_one()
    {
        var issue = Issue(
            Validate(Legal() with { Main = [.. Legal().Main.Skip(1), E("unique-gear", 2), E("unit-14", 1)] }),
            DeckIssueCode.UniqueCopies);

        Assert.Equal(2, issue.Actual);
        Assert.Equal(1, issue.Expected);
    }

    [Fact]
    public void At_most_three_signature_cards()
    {
        var deck = Legal() with
        {
            Main = [.. Legal().Main.Skip(2), E("sig-1", 1), E("sig-2", 1), E("sig-3", 1), E("sig-4", 1), E("unit-14", 2)],
        };

        Assert.Equal(4, Issue(Validate(deck), DeckIssueCode.TooManySignatures).Actual);
    }

    [Fact]
    public void Signature_cards_need_a_legend_tag() =>
        Assert.Equal(new[] { "sig-vi" }, Issue(Validate(Legal() with { Sideboard = [E("sig-vi", 1)] }), DeckIssueCode.SignatureTag).CardIds);

    [Fact]
    public void Champion_must_match_the_legend() =>
        Assert.Equal(new[] { "vi-champ" }, Issue(Validate(Legal() with { Champion = P("vi-champ") }), DeckIssueCode.ChampionMismatch).CardIds);

    [Fact]
    public void Cards_outside_the_identity_are_illegal_but_colorless_is_fine()
    {
        var outside = Validate(Legal() with { Main = [.. Legal().Main.Skip(1), E("calm-unit", 3)] });
        var colorless = Validate(Legal() with { Main = [.. Legal().Main.Skip(1), E("colorless-gear", 3)] });

        Assert.Equal(new[] { "calm-unit" }, Issue(outside, DeckIssueCode.OutsideIdentity).CardIds);
        Assert.True(colorless.IsLegal);
    }

    [Fact]
    public void Wrong_card_types_are_reported()
    {
        var deck = Legal() with
        {
            Main = [.. Legal().Main.Skip(1), E("token-recruit", 1), E("fury-rune", 1), E("unit-14", 1)],
            Battlefields = [P("bf-1"), P("bf-2"), P("unit-14")],
        };

        var wrong = Validate(deck).Issues.Where(i => i.Code == DeckIssueCode.WrongCardType).SelectMany(i => i.CardIds).Order().ToList();

        Assert.Equal(new[] { "fury-rune", "token-recruit", "unit-14" }, wrong);
    }

    [Fact]
    public void Unknown_printing_is_reported_without_throwing()
    {
        var report = Validate(Legal() with { Legend = "p-missing" });

        Assert.False(report.IsLegal);
        Assert.Equal(new[] { "p-missing" }, Issue(report, DeckIssueCode.UnknownPrinting).CardIds);
        Assert.DoesNotContain(report.Issues, i => i.Code is DeckIssueCode.ChampionMismatch or DeckIssueCode.OutsideIdentity);
    }

    [Fact]
    public void Non_positive_counts_are_invalid_and_ignored()
    {
        var report = Validate(Legal() with { Main = [.. Legal().Main, E("unit-1", -1), E("unit-14", 0)] });

        var invalid = report.Issues.Where(i => i.Code == DeckIssueCode.InvalidCount).ToList();
        Assert.Equal(2, invalid.Count);
        Assert.All(invalid, i => Assert.Equal(DeckIssueSeverity.Illegal, i.Severity));
        Assert.Equal(new[] { "p-unit-1", "p-unit-14" }, invalid.SelectMany(i => i.CardIds).Order().ToList());
        Assert.DoesNotContain(report.Issues, i => i.Code is DeckIssueCode.MainDeckSize or DeckIssueCode.TooManyCopies);
    }

    [Fact]
    public void Huge_counts_report_illegal_sizes_without_throwing()
    {
        var report = Validate(Legal() with { Sideboard = [E("unit-14", int.MaxValue), E("colorless-gear", int.MaxValue)] });

        Assert.Equal(DeckIssueSeverity.Illegal, Issue(report, DeckIssueCode.SideboardSize).Severity);
        Assert.Equal(new[] { "colorless-gear", "unit-14" },
            report.Issues.Where(i => i.Code == DeckIssueCode.TooManyCopies).SelectMany(i => i.CardIds).Order().ToList());
        Assert.DoesNotContain(report.Issues, i => i.Code == DeckIssueCode.InvalidCount);
    }
}
