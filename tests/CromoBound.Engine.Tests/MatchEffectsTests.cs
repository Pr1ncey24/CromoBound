using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class MatchEffectsTests
{
    private const string DealOne = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private static PlayerId Other(PlayerId player) => new(1 - player.Index);

    /// <summary>A legal Jinx deck with three copies of the mapped "spell" in place of filler-13.</summary>
    private static Deck SpellDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main = [.. Enumerable.Range(1, 12).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }), new DeckEntry { Printing = "p-spell", Count = 3 }],
    };

    /// <summary>To play; the first player brings a spell to hand, both players get a Recruit token, and the spell is played,
    /// stopping at its target choice. Everything goes through logged actions.</summary>
    private static (Match Match, PlayerId Player) SpellAtTargetChoice()
    {
        var db = EngineTestDb.Create(("spell", DealOne));
        var setup = new MatchSetup(MatchFormat.Bo1, SpellDeck("bf-a", "bf-b", "bf-c"), SpellDeck("bf-d", "bf-e", "bf-f"), 7);
        var match = Match.Create(setup, db).Match!.ToPlay();
        var player = match.Decision<PriorityDecision>().Player;
        var state = match.Game!.State;
        if (!state.At(Place.Hand(player)).Any(id => state[id].CardId == "spell"))
            match.Accept(player, new ManualMoveCard(state.At(Place.MainDeck(player)).First(id => state[id].CardId == "spell"), Place.Hand(player)));
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Base(player), player));
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Base(Other(player)), Other(player)));
        match.Accept(player, new PlayCard(state.At(Place.Hand(player)).First(id => state[id].CardId == "spell")));
        return (match, player);
    }

    [Fact]
    public void Undo_after_choosing_a_target_equals_never_choosing_it()
    {
        var (undone, player) = SpellAtTargetChoice();
        var (reference, _) = SpellAtTargetChoice();
        var choose = undone.Decision<ChooseTargetsDecision>();
        undone.Accept(player, new ChooseTargets { Targets = [choose.Options[0]] });

        undone.Accept(player, new RequestUndo());
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<ChooseTargetsDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_saved_match_with_target_choices_loads_identically()
    {
        var (match, player) = SpellAtTargetChoice();
        match.Accept(player, new ChooseTargets { Targets = [match.Decision<ChooseTargetsDecision>().Options[1]] });

        var json = CromoJson.Serialize(match.ToRecord());
        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(json), EngineTestDb.Create(("spell", DealOne)));

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
        var expected = match.Game!.State.Chain[0].Effect!.Targets;
        var actual = loaded.Game!.State.Chain[0].Effect!.Targets;
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) Assert.Equal(expected[i], actual[i]);
    }

    [Fact]
    public void The_opponent_sees_the_chosen_targets()
    {
        var (match, player) = SpellAtTargetChoice();
        var chosen = match.Decision<ChooseTargetsDecision>().Options[1];
        match.Accept(player, new ChooseTargets { Targets = [chosen] });

        var theirs = match.ViewFor(Other(player));

        Assert.Equal(new[] { chosen }, Assert.Single(theirs.Chain).Targets[0]);
        Assert.Contains(theirs.Log, e => e is TargetsChosen { Slot: 0 } t && t.Targets.SequenceEqual(new[] { chosen }));
    }

    [Fact]
    public void Views_show_the_target_choice_only_to_the_chooser_and_each_cards_effects_status()
    {
        var (match, player) = SpellAtTargetChoice();

        var mine = match.ViewFor(player);
        var theirs = match.ViewFor(Other(player));

        Assert.IsType<ChooseTargetsDecision>(mine.Decision);
        Assert.Null(theirs.Decision);
        Assert.Equal(new[] { player }, theirs.Deciding);
        Assert.Equal(MappingStatus.Full, Assert.Single(theirs.Chain).Card!.Effects);
        Assert.Empty(Assert.Single(theirs.Chain).Card!.ManualLines);
        Assert.All(theirs.Players[player.Index].Base, card => Assert.Equal(MappingStatus.Unmapped, card.Effects));
        Assert.Contains("\"ChooseTargets\"", CromoJson.Serialize(mine));
    }

    /// <summary>A legal Jinx deck with three Mystic Poros in place of filler-13.</summary>
    private static Deck PoroDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main = [.. Enumerable.Range(1, 12).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }), new DeckEntry { Printing = "p-mystic-poro", Count = 3 }],
    };

    private static CardDatabase PoroDb() => EngineTestDb.WithRealCards("mystic-poro");

    /// <summary>To play; the first player brings a Mystic Poro to hand, plays it with the suggested payment, and both players pass
    /// on its Vision trigger, stopping at the choice to recycle the predicted card. Everything goes through logged actions.</summary>
    private static (Match Match, PlayerId Player) AtPredictChoice()
    {
        var setup = new MatchSetup(MatchFormat.Bo1, PoroDeck("bf-a", "bf-b", "bf-c"), PoroDeck("bf-d", "bf-e", "bf-f"), 7);
        var match = Match.Create(setup, PoroDb()).Match!.ToPlay();
        var player = match.Decision<PriorityDecision>().Player;
        var state = match.Game!.State;
        if (!state.At(Place.Hand(player)).Any(id => state[id].CardId == "mystic-poro"))
            match.Accept(player, new ManualMoveCard(state.At(Place.MainDeck(player)).First(id => state[id].CardId == "mystic-poro"), Place.Hand(player)));
        match.Accept(player, new PlayCard(state.At(Place.Hand(player)).First(id => state[id].CardId == "mystic-poro")));
        var pay = match.Decision<PayCostDecision>().Suggested!;
        match.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });
        match.Accept(player, new Pass());
        match.Accept(Other(player), new Pass());
        match.Decision<OptionalDecision>();
        return (match, player);
    }

    [Fact]
    public void Undo_after_answering_a_trigger_choice_equals_never_answering()
    {
        var (undone, player) = AtPredictChoice();
        var (reference, _) = AtPredictChoice();
        undone.Accept(player, new ChooseOptional(true));

        undone.Accept(player, new RequestUndo());
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<OptionalDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_saved_match_with_a_trigger_and_an_effect_choice_loads_identically()
    {
        var (match, player) = AtPredictChoice();
        match.Accept(player, new ChooseOptional(true));

        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(CromoJson.Serialize(match.ToRecord())), PoroDb());

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
    }

    [Fact]
    public void Only_the_predicting_player_sees_the_predicted_card()
    {
        var (match, player) = AtPredictChoice();

        var mine = match.ViewFor(player);
        var theirs = match.ViewFor(Other(player));

        Assert.NotNull(Assert.Single(mine.Log.OfType<Predicted>(), p => p.CardIds is not null).CardIds);
        Assert.Null(Assert.Single(theirs.Log.OfType<Predicted>()).CardIds);
        Assert.IsType<OptionalDecision>(mine.Decision);
        Assert.Null(theirs.Decision);
        Assert.Equal("Optional", theirs.DecisionKind);
    }

    /// <summary>Mapped Fury and Chaos cards from data/: Accelerate, Ambush, Assault, Deflect, Legion, Vision and Weaponmaster.</summary>
    private static readonly string[] MappedFuryChaos =
    [
        "legion-rearguard", "inferna", "pouty-poro", "sharkling", "sentinel-adept",
        "mystic-poro", "shipyard-skulker", "noxus-hopeful", "blazing-scorcher",
    ];

    /// <summary>A legal Jinx deck: the nine mapped cards and filler-1 to filler-4, three copies each.</summary>
    private static Deck MappedDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main =
        [
            .. MappedFuryChaos.Select(id => new DeckEntry { Printing = $"p-{id}", Count = 3 }),
            .. Enumerable.Range(1, 4).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }),
        ],
    };

    [Fact]
    public void Scripted_players_finish_a_bo3_with_mapped_cards_and_the_saved_match_replays_identically()
    {
        var setup = new MatchSetup(MatchFormat.Bo3, MappedDeck("bf-a", "bf-b", "bf-c"), MappedDeck("bf-d", "bf-e", "bf-f"), 11);
        var match = Match.Create(setup, EngineTestDb.WithRealCards(MappedFuryChaos)).Match!;
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 20000 && match.Stage != MatchStage.Over; i++)
        {
            var player = match.Pending!.Players[0];
            match.Accept(player, bots[player.Index].Choose(match));
        }

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Contains(2, match.Result.GameWins);
        Assert.Contains(match.Events, e => e is TriggerAdded);
        var loaded = Match.Load(match.ToRecord(), EngineTestDb.WithRealCards(MappedFuryChaos));
        var first = new PlayerId(0);
        Assert.Equal(CromoJson.Serialize(match.ViewFor(first)), CromoJson.Serialize(loaded.ViewFor(first)));
    }
}
