using CromoBound.Client.Board;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class BoardTurnTests
{
    [Fact]
    public void A_playable_card_glows_and_a_click_plays_it()
    {
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");
        var twirler = board.Add(board.Hand, "unit-a");

        var model = board.Model(TestBoard.Priority(playable: [poro.Id]));

        Assert.Equal((true, Ring.Legal), (model.Me.Hand[0].Clickable, model.Me.Hand[0].Ring));
        Assert.Equal((false, Ring.None), (model.Me.Hand[1].Clickable, model.Me.Hand[1].Ring));
        Assert.Equal(new SendStep(new PlayCard(poro.Id)), model.Click(poro.Id));
        Assert.IsType<NoStep>(model.Click(twirler.Id));
    }

    [Fact]
    public void A_card_with_several_options_opens_its_menu()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-a", might: 2);

        var model = board.Model(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(0)])], activations: [new ActivateOption(unit.Id, 0)]));

        var menu = Assert.IsType<MenuStep>(model.Click(unit.Id));
        Assert.Equal(new[] { "Move", "Use ability 1" }, menu.Items.Select(i => i.Label));
        Assert.Equal(new[] { unit.Id }, Assert.IsType<Moving>(Assert.IsType<NextStep>(menu.Items[0].Step).Next).Units);
        Assert.Equal(new SendStep(new ActivateAbility(unit.Id, 0)), menu.Items[1].Step);
    }

    [Fact]
    public void A_hidden_card_offers_to_be_played_or_hidden_at_each_battlefield()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var card = board.Add(board.Hand, "spell-a");

        var model = board.Model(TestBoard.Priority(playable: [card.Id], hides: [new HideOption(card.Id, [0, 1])]));

        var menu = Assert.IsType<MenuStep>(model.Click(card.Id));
        Assert.Equal(new[] { "Play", "Hide at Back-Alley Bar", "Hide at Altar to Unity" }, menu.Items.Select(i => i.Label));
        Assert.Equal(new SendStep(new Hide(card.Id, 1)), menu.Items[2].Step);
    }

    [Fact]
    public void A_lone_ability_or_hide_still_opens_the_menu()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        var card = board.Add(board.Hand, "spell-a");

        var model = board.Model(TestBoard.Priority(activations: [new ActivateOption(unit.Id, 0)], hides: [new HideOption(card.Id, [0])]));

        var ability = Assert.IsType<MenuStep>(model.Click(unit.Id));
        Assert.Equal(new[] { "Use ability 1" }, ability.Items.Select(i => i.Label));
        var hide = Assert.IsType<MenuStep>(model.Click(card.Id));
        Assert.Equal(new[] { "Hide at Back-Alley Bar" }, hide.Items.Select(i => i.Label));
    }

    [Fact]
    public void A_rune_always_asks_how_to_use_it()
    {
        var board = new TestBoard();
        var ready = board.Add(board.MyBase, "fury-rune");
        var spent = board.Add(board.MyBase, "order-rune", exhausted: true);

        var model = board.Model(TestBoard.Priority(runes: [new RuneOption(ready.Id, true), new RuneOption(spent.Id, false)]));

        var readyMenu = Assert.IsType<MenuStep>(model.Click(ready.Id));
        Assert.Equal(new[] { "Exhaust for 1 energy", "Recycle for 1 power" }, readyMenu.Items.Select(i => i.Label));
        Assert.Equal(new SendStep(new UseRune(ready.Id, RuneUse.Exhaust)), readyMenu.Items[0].Step);
        var spentMenu = Assert.IsType<MenuStep>(model.Click(spent.Id));
        Assert.Equal(new SendStep(new UseRune(spent.Id, RuneUse.Recycle)), Assert.Single(spentMenu.Items).Step);
    }

    [Theory]
    [InlineData(true, true, "PASS")]
    [InlineData(false, true, "END TURN")]
    public void The_big_button_passes_or_ends_the_turn(bool canPass, bool canEndTurn, string label)
    {
        var model = new TestBoard().Model(TestBoard.Priority(canPass: canPass, canEndTurn: canEndTurn));

        Assert.Equal((label, true), (model.Button.Label, model.Button.Enabled));
        Assert.Equal(canPass ? new SendStep(new Pass()) : new SendStep(new EndTurn()), model.Button.Step);
    }

    [Fact]
    public void With_neither_the_big_button_is_off()
    {
        var model = new TestBoard().Model(TestBoard.Priority(canPass: false, canEndTurn: false));

        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void Moving_a_unit_highlights_where_it_can_go_and_a_click_there_moves_it()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var unit = board.Add(board.MyBase, "unit-b", might: 1);
        var priority = TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(1)])]);

        var model = board.Model(priority, new Moving([unit.Id]));

        Assert.Equal(Ring.Selected, model.Me.Base.Single().Ring);
        Assert.Equal(new[] { false, true }, model.Lanes.Select(l => l.IsDestination));
        Assert.False(model.Me.BaseIsDestination);
        Assert.Equal("Moving Daring Poro: pick a destination", model.Hint);
        Assert.False(model.Button.Enabled);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(model.ClickLane(1)).Action);
        Assert.Equal(new[] { unit.Id }, move.Units);
        Assert.Equal(Place.Battlefield(1), move.Destination);
        Assert.IsType<NoStep>(model.ClickLane(0));
        Assert.Equal(new NextStep(Idle.Instance), model.Cancel());
        Assert.Equal(new NextStep(Idle.Instance), model.Click(unit.Id));
    }

    [Fact]
    public void Adding_a_unit_keeps_only_the_destinations_all_share()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var first = board.Add(board.MyBase, "unit-a", might: 2);
        var second = board.Add(board.MyBase, "unit-b", might: 1);
        var third = board.Add(board.MyBase, "jinx-champ", might: 3);
        var priority = TestBoard.Priority(moves:
        [
            new MoveOption(first.Id, [Place.Battlefield(0), Place.Battlefield(1)]),
            new MoveOption(second.Id, [Place.Battlefield(1)]),
            new MoveOption(third.Id, [Place.Base(TestBoard.Me)]),
        ]);

        var one = board.Model(priority, new Moving([first.Id]));
        var added = Assert.IsType<Moving>(Assert.IsType<NextStep>(one.Click(second.Id)).Next);
        Assert.Equal(new[] { first.Id, second.Id }, added.Units);
        Assert.IsType<NoStep>(one.Click(third.Id));

        var two = board.Model(priority, added);
        Assert.Equal(new[] { false, true }, two.Lanes.Select(l => l.IsDestination));
        Assert.Equal("Moving 2 units: pick a destination", two.Hint);
        var removed = Assert.IsType<Moving>(Assert.IsType<NextStep>(two.Click(second.Id)).Next);
        Assert.Equal(new[] { first.Id }, removed.Units);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(two.ClickLane(1)).Action);
        Assert.Equal(new[] { first.Id, second.Id }, move.Units);
    }

    [Fact]
    public void A_unit_can_move_to_its_base()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.AddToLane(0, "unit-b", TestBoard.Me, might: 1);

        var model = board.Model(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Base(TestBoard.Me)])]), new Moving([unit.Id]));

        Assert.True(model.Me.BaseIsDestination);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(model.ClickBase()).Action);
        Assert.Equal(Place.Base(TestBoard.Me), move.Destination);
    }

    [Fact]
    public void A_move_whose_units_can_no_longer_move_is_dropped()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-b", might: 1);

        var model = board.Model(TestBoard.Priority(), new Moving([unit.Id]));

        Assert.Null(model.Hint);
        Assert.Equal(Ring.None, model.Me.Base.Single().Ring);
        Assert.True(model.Button.Enabled);
    }

    [Fact]
    public void Play_options_ask_where_the_card_enters_and_about_accelerate()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var card = board.Add(board.Hand, "unit-a");

        var model = board.Model(new PlayChoicesDecision(TestBoard.Me, card.Id, [Place.Base(TestBoard.Me), Place.Battlefield(0)], true));

        var panel = Assert.IsType<PlayOptionsPanel>(model.Panel);
        Assert.Equal("Blade Twirler", panel.CardName);
        Assert.Equal(new[] { new PlaceOption(Place.Base(TestBoard.Me), "Your base"), new PlaceOption(Place.Battlefield(0), "Back-Alley Bar") }, panel.Locations);
        Assert.True(panel.AccelerateAvailable);
        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void Paying_starts_from_the_suggestion_and_pay_sends_it()
    {
        var board = new TestBoard();
        var a = board.Add(board.MyBase, "fury-rune");
        var b = board.Add(board.MyBase, "fury-rune");
        var c = board.Add(board.MyBase, "order-rune");
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(2, [PowerSymbol.Order]), [], new PaymentSuggestion([a.Id, b.Id], [c.Id]));

        var model = board.Model(pay);

        Assert.Equal(new[] { PayMark.Exhaust, PayMark.Exhaust, PayMark.Recycle }, model.Me.Runes.Select(r => r.Mark));
        Assert.All(model.Me.Runes, r => Assert.Equal(Ring.Suggested, r.Ring));
        Assert.Equal(("PAY", "2 energy and 1 Order"), (model.Button.Label, model.Button.Detail));
        var paid = Assert.IsType<PayCost>(Assert.IsType<SendStep>(model.Button.Step).Action);
        Assert.Equal(new[] { a.Id, b.Id }, paid.Exhaust);
        Assert.Equal(new[] { c.Id }, paid.Recycle);
        Assert.Equal(new[] { "Cancel", "Suggest" }, model.Extras.Select(e => e.Label));
        Assert.Equal(new SendStep(new CancelPlay()), model.Extras[0].Step);
    }

    [Fact]
    public void A_rune_cycles_exhaust_both_recycle_unused()
    {
        var board = new TestBoard();
        var ready = board.Add(board.MyBase, "fury-rune");
        var spent = board.Add(board.MyBase, "fury-rune", exhausted: true);
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(1, []), [], null);
        Paying Next(BoardModel model, CardView rune) => Assert.IsType<Paying>(Assert.IsType<NextStep>(model.Click(rune.Id)).Next);

        var start = board.Model(pay);
        Assert.Equal(new[] { PayMark.None, PayMark.None }, start.Me.Runes.Select(r => r.Mark));

        var exhaust = Next(start, ready);
        Assert.Equal(RunePay.Exhaust, exhaust.Uses[ready.Id]);
        var both = Next(board.Model(pay, exhaust), ready);
        Assert.Equal(RunePay.Both, both.Uses[ready.Id]);
        Assert.Equal(PayMark.Both, board.Model(pay, both).Me.Runes[0].Mark);
        var recycle = Next(board.Model(pay, both), ready);
        Assert.Equal(RunePay.Recycle, recycle.Uses[ready.Id]);
        var unused = Next(board.Model(pay, recycle), ready);
        Assert.False(unused.Uses.ContainsKey(ready.Id));

        var spentRecycle = Next(start, spent);
        Assert.Equal(RunePay.Recycle, spentRecycle.Uses[spent.Id]);
        Assert.False(Next(board.Model(pay, spentRecycle), spent).Uses.ContainsKey(spent.Id));
        Assert.Equal(new[] { "Cancel" }, start.Extras.Select(e => e.Label));
    }

    [Fact]
    public void Five_runes_pay_five_energy_and_one_power_by_exhausting_and_recycling_one_rune()
    {
        var board = new TestBoard();
        var runes = Enumerable.Range(0, 5).Select(_ => board.Add(board.MyBase, "fury-rune")).ToList();
        var ids = runes.Select(r => r.Id).ToList();
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(5, [PowerSymbol.Fury]), [], new PaymentSuggestion(ids, [ids[0]]));

        var model = board.Model(pay);

        Assert.Equal(new[] { PayMark.Both, PayMark.Exhaust, PayMark.Exhaust, PayMark.Exhaust, PayMark.Exhaust }, model.Me.Runes.Select(r => r.Mark));
        var paid = Assert.IsType<PayCost>(Assert.IsType<SendStep>(model.Button.Step).Action);
        Assert.Equal(ids, paid.Exhaust);
        Assert.Equal(new[] { ids[0] }, paid.Recycle);
    }

    [Fact]
    public void Suggest_puts_the_suggestion_back()
    {
        var board = new TestBoard();
        var a = board.Add(board.MyBase, "fury-rune");
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(1, []), [], new PaymentSuggestion([a.Id], []));

        var model = board.Model(pay, new Paying(new Dictionary<ObjectId, RunePay>()));

        Assert.Equal(PayMark.None, model.Me.Runes.Single().Mark);
        var back = Assert.IsType<Paying>(Assert.IsType<NextStep>(model.Extras[1].Step).Next);
        Assert.Equal(RunePay.Exhaust, back.Uses[a.Id]);
    }

    [Theory]
    [InlineData(0, new PowerSymbol[0], "Free")]
    [InlineData(3, new PowerSymbol[0], "3 energy")]
    [InlineData(4, new[] { PowerSymbol.Fury }, "4 energy and 1 Fury")]
    [InlineData(2, new[] { PowerSymbol.Fury, PowerSymbol.Fury, PowerSymbol.Any }, "2 energy, 2 Fury and 1 of any domain")]
    [InlineData(0, new[] { PowerSymbol.Self }, "1 of its domain")]
    public void Costs_read_as_words(int energy, PowerSymbol[] power, string text)
    {
        Assert.Equal(text, BoardModel.CostText(new TotalCost(energy, power)));
    }
}
