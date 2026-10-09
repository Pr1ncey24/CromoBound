using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ChoiceTests
{
    private static readonly Filter Opponent = new() { Relation = Relation.Opponent };

    private const string ChooseThenDraw = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "ChoosePlayer", "store": "who" },
          { "action": "Draw", "amount": 2, "player": { "var": "who" } } ] } ] }
        """;

    /// <summary>Starts running the steps for P1; they may stop at a decision. Returns the context and the events so far.</summary>
    private static (EffectContext Context, IReadOnlyList<GameEvent> Events) Run(Game engine, params Step[] steps)
    {
        var context = new EffectContext { Controller = P1, SourceCardId = "spell" };
        var events = engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        return (context, events);
    }

    [Fact]
    public void Choosing_an_opponent_when_there_is_one_is_forced_and_announced()
    {
        var engine = new TestGame().Start();

        var (context, events) = Run(engine, new ChoosePlayerStep { Filter = Opponent, Store = "victim" });

        Assert.Equal(new[] { P2 }, context.Vars["victim"].Players);
        Assert.Contains(events, e => e is ChoiceMade { Kind: "Player" });
        Assert.Contains(events, e => e is PlayerChosen { Player.Index: 0, Chosen.Index: 1 });
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Choosing_among_several_players_asks_and_stores_the_answer()
    {
        var game = new TestGame();
        var engine = game.Start();

        var (context, _) = Run(engine,
            new ChoosePlayerStep { Store = "who" },
            new DrawStep { Amount = 1, Player = new PlayerRef { Var = "who" } });

        var choose = engine.Decision<ChoosePlayerDecision>();
        Assert.Equal(new[] { P1, P2 }, choose.Options);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new ChoosePlayer(new PlayerId(5))).Rejection!.Code);
        var result = engine.Accept(P1, new ChoosePlayer(P2));

        Assert.Equal(new[] { P2 }, context.Vars["who"].Players);
        Assert.Single(game.State.At(Place.Hand(P2)));
        Assert.Contains(result.Events, e => e is PlayerChosen { Chosen.Index: 1 });
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_manual_action_during_an_effect_choice_asks_again()
    {
        var engine = new TestGame().Start();
        Run(engine, new ChoosePlayerStep { Store = "who" });
        engine.Decision<ChoosePlayerDecision>();

        Assert.True(engine.SubmitManual(P1, new ManualAdjustXp(P1, 1)).Accepted);

        Assert.IsType<ChoosePlayerDecision>(engine.Pending);
    }

    [Fact]
    public void Countering_a_spell_while_its_effect_waits_for_a_choice_stops_the_effect()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", ChooseThenDraw)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Decision<ChoosePlayerDecision>();
        var item = Assert.Single(game.State.Chain);
        var hands = (game.State.At(Place.Hand(P1)).Count, game.State.At(Place.Hand(P2)).Count);

        Assert.True(engine.SubmitManual(P2, new ManualCounter(item.Id)).Accepted);

        Assert.Empty(game.State.Chain);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Equal(hands, (game.State.At(Place.Hand(P1)).Count, game.State.At(Place.Hand(P2)).Count));
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Choosing_cards_from_a_trash_offers_only_matching_cards()
    {
        var game = new TestGame();
        var units = new[] { game.Put("unit-2", Place.Trash(P2)), game.Put("unit-3", Place.Trash(P2)) };
        game.Put("spell", Place.Trash(P2));
        game.Put("unit-2", Place.Trash(P1));
        var engine = game.Start();
        var trash = new ZoneRef { Zone = Zone.Trash, Owner = new PlayerRef { Kind = PlayerKind.Opponent } };

        var (context, _) = Run(engine, new ChooseCardStep { From = trash, Filter = new Filter { Type = CardType.Unit }, Store = "picked" });

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(units.ToHashSet(), choose.Options.ToHashSet());
        Assert.Equal((1, 1), (choose.Min, choose.Max));
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseCards { Cards = [units[0], units[1]] }).Rejection!.Code);
        engine.Accept(P1, new ChooseCards { Cards = [units[1]] });

        Assert.Equal(new[] { units[1] }, context.Vars["picked"].Objects);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Exactly_enough_cards_is_a_forced_choice_and_none_is_nothing()
    {
        var game = new TestGame();
        var only = game.Put("unit-2", Place.Trash(P1));
        var engine = game.Start();
        var mine = new ZoneRef { Zone = Zone.Trash };

        var (context, events) = Run(engine,
            new ChooseCardStep { From = mine, Store = "one" },
            new ChooseCardStep { From = mine, Filter = new Filter { Type = CardType.Spell }, Store = "none" });

        Assert.Equal(new[] { only }, context.Vars["one"].Objects);
        Assert.Contains(events, e => e is ChoiceMade { Kind: "Cards" });
        Assert.False(context.Vars["none"].Happened);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Saying_yes_to_a_reflexive_block_puts_it_on_the_chain_with_the_same_context()
    {
        var game = new TestGame();
        var engine = game.Start();
        var (context, _) = Run(engine,
            new ChoosePlayerStep { Filter = Opponent, Store = "victim" },
            new OptionalStep { Reflexive = true, Steps = [new BurnStep { Amount = 2, Player = new PlayerRef { Var = "victim" } }] });

        var ask = engine.Decision<OptionalDecision>();
        Assert.Equal(P1, ask.Player);
        Assert.Equal(OptionalHandler.Question, ask.Text);
        engine.Accept(P1, new ChooseOptional(true));

        var block = Assert.Single(game.State.Chain);
        Assert.Same(context, block.Effect);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        Assert.Equal(2, game.State.At(Place.Trash(P2)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Saying_no_to_a_reflexive_block_skips_it()
    {
        var game = new TestGame();
        var engine = game.Start();
        var (context, _) = Run(engine, new OptionalStep { Reflexive = true, Steps = [new DrawStep { Amount = 1 }], Store = "did" });

        engine.Accept(P1, new ChooseOptional(false));

        Assert.Empty(game.State.Chain);
        Assert.False(context.Vars["did"].Happened);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Predicting_shows_the_top_card_only_to_the_player_who_may_recycle_it()
    {
        var game = new TestGame();
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);

        var (_, events) = Run(engine, new PredictStep());

        var predicted = events.OfType<Predicted>().ToList();
        Assert.Equal(2, predicted.Count);
        Assert.Equal(new[] { "unit-3" }, predicted.Single(p => p.VisibleTo == P1).CardIds);
        Assert.Null(predicted.Single(p => p.VisibleTo is null).CardIds);
        Assert.Equal(PredictHandler.Question, engine.Decision<OptionalDecision>().Text);
        engine.Accept(P1, new ChooseOptional(true));

        var deck = game.State.At(Place.MainDeck(P1));
        Assert.Equal("unit-3", game.State[deck[^1]].CardId);
        Assert.Equal("unit-2", game.State[deck[0]].CardId);
    }

    [Fact]
    public void Declining_a_prediction_keeps_the_card_on_top()
    {
        var game = new TestGame();
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);
        Run(engine, new PredictStep());

        engine.Accept(P1, new ChooseOptional(false));

        Assert.Equal("unit-3", game.State[game.State.At(Place.MainDeck(P1))[0]].CardId);
    }
}
