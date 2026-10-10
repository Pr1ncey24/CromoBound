using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Matches;

namespace CromoBound.Client.Tests;

public class LobbyPageTests
{
    private static readonly Guid FromGiulia = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private static async Task<Ui> LobbyAsync(
        IReadOnlyList<PlayerPresence>? players = null, IReadOnlyList<ChallengeInfo>? challenges = null, Guid? match = null,
        bool maintenance = false, HubState connection = HubState.Connected, int decks = 2)
    {
        var ui = new Ui();
        if (decks > 0) await ui.Decks.AddAsync(SampleDecks.Json("Jinx aggro"));
        if (decks > 1) await ui.Decks.AddAsync(SampleDecks.Json("Vi midrange"));
        ui.Lobby.Load(new LobbyReply(
            players ?? [new("giulia", true, false), new("sara", true, true), new("tommaso", false, false)],
            challenges ?? [], match, null, maintenance), null);
        ui.Lobby.SetConnection(connection);
        return ui;
    }

    private static bool CanChallenge(IRenderedComponent<Lobby> cut, string player) =>
        !cut.Find($"[data-player='{player}'] .challenge").HasAttribute("disabled");

    [Fact]
    public async Task Players_show_their_status_and_who_can_be_challenged()
    {
        await using var ui = await LobbyAsync();
        var cut = ui.Ctx.Render<Lobby>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".cb-player").Count));
        Assert.Equal("Pick an opponent and a deck. 2 of your friends are online.", cut.Find(".cb-sub").TextContent);
        Assert.Equal(new[] { "Online", "In a match", "Offline" }, cut.FindAll(".cb-status").Select(s => s.TextContent.Trim()));
        Assert.True(CanChallenge(cut, "giulia"));
        Assert.False(CanChallenge(cut, "sara"));
        Assert.True(CanChallenge(cut, "tommaso"));
        Assert.Contains("Your decks (2)", cut.Markup);
        Assert.Equal("You haven't challenged anyone.", cut.Find(".cb-sent .cb-sub").TextContent);
    }

    [Fact]
    public async Task Challenging_sends_the_chosen_format_and_deck()
    {
        await using var ui = await LobbyAsync();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();
        var vi = (await ui.Decks.ListAsync())[1];

        await cut.ClickAsync("[data-player='giulia'] .challenge");
        dialogs.WaitForElement("#send-challenge");
        await dialogs.InvokeAsync(() => dialogs.Find("#bo3").Change(true));
        await dialogs.InvokeAsync(() => dialogs.Find("#deck").Change(vi.Id));
        await dialogs.ClickAsync("#send-challenge");

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Challenge giulia Bo3 Vi midrange" }, ui.Hub.Calls));
        cut.WaitForAssertion(() => Assert.Equal("You challenged giulia to a best of three. Waiting for an answer.", cut.Find(".cb-sent div").TextContent));
        Assert.False(CanChallenge(cut, "tommaso"));
        Assert.Contains("You have an open challenge. Cancel it to challenge someone else.", cut.Markup);
    }

    [Fact]
    public async Task While_a_challenge_is_being_sent_every_challenge_button_is_off()
    {
        await using var ui = await LobbyAsync();
        var hold = ui.Hub.Hold = new TaskCompletionSource();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("[data-player='giulia'] .challenge");
        await dialogs.ClickAsync("#send-challenge");

        cut.WaitForAssertion(() => Assert.Single(ui.Hub.Calls));
        cut.WaitForAssertion(() => Assert.All(cut.FindAll(".challenge"), b => Assert.True(b.HasAttribute("disabled"))));
        hold.SetResult();

        cut.WaitForAssertion(() => Assert.Equal("You challenged giulia to a best of one. Waiting for an answer.", cut.Find(".cb-sent div").TextContent));
        Assert.Single(ui.Hub.Calls);
    }

    [Fact]
    public async Task A_refused_deck_shows_the_servers_problems_and_lets_you_pick_another()
    {
        await using var ui = await LobbyAsync();
        ui.Hub.Replies["Challenge"] = new HubReply(null, "That deck isn't legal.",
            [new DeckIssue(DeckIssueCode.MainDeckSize, DeckIssueSeverity.Incomplete, [], "The main deck has 38 cards; it needs 40.")]);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("[data-player='giulia'] .challenge");
        await dialogs.ClickAsync("#send-challenge");
        dialogs.WaitForAssertion(() => Assert.Equal("The main deck has 38 cards; it needs 40.", dialogs.Find(".cb-problems li").TextContent));
        Assert.Contains("Jinx aggro", dialogs.Find(".cb-problems").TextContent);
        await dialogs.ClickAsync("#pick-another");
        await dialogs.ClickAsync("#send-challenge");

        cut.WaitForAssertion(() => Assert.Equal(2, ui.Hub.Calls.Count(c => c.StartsWith("Challenge giulia", StringComparison.Ordinal))));
        cut.WaitForAssertion(() => Assert.NotNull(ui.Lobby.Sent));
    }

    [Fact]
    public async Task Without_decks_the_challenge_dialog_points_to_the_decks_page()
    {
        await using var ui = await LobbyAsync(decks: 0);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("[data-player='giulia'] .challenge");

        dialogs.WaitForAssertion(() => Assert.Contains("You have no decks yet.", dialogs.Markup));
        Assert.True(dialogs.Find("#send-challenge").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Accepting_sends_your_deck_and_declining_from_the_dialog_declines()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo3)]);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();
        cut.WaitForAssertion(() => Assert.Contains("challenges you", cut.Find(".cb-received").TextContent));
        Assert.Equal("Best of three", cut.Find(".cb-received .cb-chip").TextContent);

        await cut.ClickAsync(".cb-received .accept");
        dialogs.WaitForAssertion(() => Assert.Contains("giulia challenges you to a best of three. Pick the deck you play with.", dialogs.Markup));
        await dialogs.ClickAsync("#accept-and-play");
        cut.WaitForAssertion(() => Assert.Contains($"Accept {FromGiulia} Jinx aggro", ui.Hub.Calls));

        await cut.ClickAsync(".cb-received .accept");
        await dialogs.ClickAsync("#decline-in-dialog");
        cut.WaitForAssertion(() => Assert.Contains($"Decline {FromGiulia}", ui.Hub.Calls));
    }

    [Fact]
    public async Task Cancelling_your_challenge_and_declining_from_the_ticket_call_the_hub()
    {
        var sent = Guid.NewGuid();
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1), new(sent, "marco", "luca", MatchFormat.Bo1)]);
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("#cancel-challenge");
        await cut.ClickAsync(".cb-received .decline");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Cancel {sent}", $"Decline {FromGiulia}" }, ui.Hub.Calls));
    }

    [Fact]
    public async Task A_refused_call_shows_the_message_and_reloads_the_lobby()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)]);
        ui.Hub.Replies["Decline"] = HubReply.Fail("There is no such challenge.");
        ui.Hub.Lobby = new LobbyReply([new("giulia", true, false)], [], null, null, false);
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync(".cb-received .decline");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-received")));
        Assert.Contains("GetLobby", ui.Hub.Calls);
        Assert.Contains("There is no such challenge.", ui.Notices);
    }

    [Fact]
    public async Task While_reconnecting_the_banner_shows_and_challenging_is_off()
    {
        await using var ui = await LobbyAsync(
            challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)], connection: HubState.Reconnecting);

        var cut = ui.RenderInLayout<Lobby>();

        cut.WaitForAssertion(() => Assert.Contains("Reconnecting...", cut.Find(".cb-banner.warn").TextContent));
        Assert.All(cut.FindAll(".challenge"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".cb-received .accept").HasAttribute("disabled"));
        Assert.True(cut.Find(".cb-received .decline").HasAttribute("disabled"));
    }

    [Fact]
    public async Task In_maintenance_challenging_and_accepting_are_off()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)], maintenance: true);
        var cut = ui.Ctx.Render<Lobby>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".cb-player").Count));
        Assert.All(cut.FindAll(".challenge"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".cb-received .accept").HasAttribute("disabled"));
        Assert.False(cut.Find(".cb-received .decline").HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_running_match_takes_you_to_its_page()
    {
        var match = Guid.NewGuid();
        await using var ui = await LobbyAsync(match: match);

        ui.Ctx.Render<Lobby>();

        Assert.EndsWith($"/match/{match}", ui.Nav.Uri);
        Assert.True(ui.Nav.History.First().Options.ReplaceHistoryEntry);
    }
}
