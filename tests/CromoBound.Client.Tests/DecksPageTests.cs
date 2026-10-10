using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace CromoBound.Client.Tests;

public class DecksPageTests
{
    [Fact]
    public async Task Pasting_a_deck_adds_it_to_the_list()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.Find("#paste").Input(SampleDecks.Json("Jinx aggro"));
        await cut.ClickAsync("#add-deck");

        cut.WaitForAssertion(() => Assert.Equal("Jinx aggro", cut.Find(".cb-deck-name").TextContent));
        Assert.Equal("40 main · 12 runes · 3 battlefields", cut.Find(".cb-deck-counts").TextContent);
        Assert.Equal("", cut.Find("#paste").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Pasting_garbage_shows_why_and_keeps_the_text()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.Find("#paste").Input("{ broken");
        await cut.ClickAsync("#add-deck");

        cut.WaitForAssertion(() => Assert.Equal(DeckStore.NotJson, cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll(".cb-deckbox"));
        Assert.Empty(ui.Storage.Items);
    }

    [Fact]
    public async Task An_uploaded_file_fills_the_box()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(SampleDecks.Json("From a file"), "deck.json"));

        cut.WaitForAssertion(() => Assert.Contains("From a file", cut.Find("#paste").GetAttribute("value")));
    }

    [Fact]
    public async Task A_file_that_is_too_big_is_refused()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[DeckStore.MaxBytes + 1], "big.json"));

        cut.WaitForAssertion(() => Assert.Equal(DeckStore.TooBig, cut.Find("[role=alert]").TextContent));
        Assert.Equal("", cut.Find("#paste").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task A_deck_is_renamed_in_place_and_deleted()
    {
        await using var ui = new Ui();
        await ui.Decks.AddAsync(SampleDecks.Json("Old name"));
        var cut = ui.Ctx.Render<Decks>();
        cut.WaitForAssertion(() => Assert.Equal("Old name", cut.Find(".cb-deck-name").TextContent));

        await cut.ClickAsync(".rename");
        cut.Find(".rename-input").Input("New name");
        await cut.ClickAsync(".rename-save");
        cut.WaitForAssertion(() => Assert.Equal("New name", cut.Find(".cb-deck-name").TextContent));

        await cut.ClickAsync(".delete");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-deckbox")));
        Assert.Empty(await ui.Decks.ListAsync());
    }
}
