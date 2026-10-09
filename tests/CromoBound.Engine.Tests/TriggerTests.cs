using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TriggerTests
{
    /// <summary>unit-3 with Deathknell: draw 1.</summary>
    private const string DrawOnDeath = """
        { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Deathknell", "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
        """;

    private const string KillThenDraw = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Kill", "target": { "select": "Unit", "count": 1 } }, { "action": "Draw", "amount": 1 } ] } ] }
        """;

    /// <summary>The priority holder passes, then the other player: the newest chain item resolves.</summary>
    private static SubmitResult BothPass(Game engine)
    {
        var first = engine.Decision<PriorityDecision>().Player;
        engine.Accept(first, new Pass());
        return engine.Accept(new PlayerId(1 - first.Index), new Pass());
    }

    [Fact]
    public void Soaring_scout_channels_a_rune_exhausted_when_it_dies()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soaring-scout"));
        for (var i = 0; i < 3; i++) game.Put("fury-rune", Place.RuneDeck(P1));
        var scout = game.Put("soaring-scout", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.SubmitManual(P1, new ManualDamage(scout, 1)).Accepted);

        var trigger = Assert.Single(game.State.Chain);
        Assert.Equal("soaring-scout", trigger.SourceCardId);
        Assert.Equal(AbilityKind.Triggered, trigger.AbilityKind);
        Assert.Equal(P1, trigger.Controller);
        BothPass(engine);
        Assert.Equal(3, engine.RunesOf(P1).Count);
        Assert.Equal(1, engine.RunesOf(P1).Count(r => r.Exhausted));
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Mystic_poro_predicts_when_played()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("mystic-poro"));
        game.Put("mystic-poro", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 2);
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "mystic-poro")));
        engine.PayWithSuggestion(P1);

        Assert.Equal("mystic-poro", Assert.Single(game.State.Chain).SourceCardId);
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);
        var result = BothPass(engine);

        Assert.Equal(new[] { "unit-3" }, Assert.Single(result.Events.OfType<Predicted>(), p => p.VisibleTo == P1).CardIds);
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Equal("unit-3", game.State[game.State.At(Place.MainDeck(P1))[^1]].CardId);
    }

    [Fact]
    public void Voracious_gromp_gains_3_xp_when_its_controller_holds_its_battlefield()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("voracious-gromp"));
        game.Put("voracious-gromp", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start();

        Assert.Equal("voracious-gromp", Assert.Single(game.State.Chain).SourceCardId);
        BothPass(engine);
        Assert.Equal(3, game.State.Player(P1).Xp);
        Assert.Equal(1, game.State.Player(P1).Points);
    }

    [Fact]
    public void Voracious_gromp_gains_3_xp_when_its_controller_conquers_its_battlefield()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("voracious-gromp"));
        game.Put("voracious-gromp", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start(first: P2);

        engine.RunNow(new StepTask(g => g.Score(P1, 0, ScoreKind.Conquer)));

        Assert.Equal("voracious-gromp", Assert.Single(game.State.Chain).SourceCardId);
        BothPass(engine);
        Assert.Equal(3, game.State.Player(P1).Xp);
    }

    [Fact]
    public void Shadow_temple_burns_3_when_you_hold_it()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("shadow-temple"), firstBattlefield: "shadow-temple");
        game.Put("unit-2", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start();

        var trigger = Assert.Single(game.State.Chain);
        Assert.Equal("shadow-temple", trigger.SourceCardId);
        Assert.Equal(P1, trigger.Controller);
        Assert.StartsWith("When you hold here", trigger.Text);
        BothPass(engine);
        Assert.Equal(3, game.State.At(Place.Trash(P1)).Count);
    }

    [Fact]
    public void A_player_orders_their_own_simultaneous_triggers()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", DrawOnDeath)));
        var first = game.Put("unit-3", Place.Base(P1));
        var second = game.Put("unit-3", Place.Base(P1));
        game.State[first].Damage = 3;
        game.State[second].Damage = 3;
        var engine = game.Start();

        var order = engine.Decision<OrderTriggersDecision>();
        Assert.Equal(new[] { first, second }, order.Triggers.Select(t => t.Source));
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new OrderTriggers { Order = [0, 0] }).Rejection!.Code);
        engine.Accept(P1, new OrderTriggers { Order = [1, 0] });

        Assert.Equal(new ObjectId?[] { second, first }, game.State.Chain.Select(i => i.Source));
    }

    [Fact]
    public void The_turn_players_triggers_go_on_the_chain_first_so_the_opponents_resolve_first()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", DrawOnDeath)));
        var mine = game.Put("unit-3", Place.Base(P1));
        var theirs = game.Put("unit-3", Place.Base(P2));
        game.State[mine].Damage = 3;
        game.State[theirs].Damage = 3;
        var engine = game.Start();

        Assert.Equal(new[] { P1, P2 }, game.State.Chain.Select(i => i.Controller));
        var hand = game.State.At(Place.Hand(P2)).Count;
        BothPass(engine);
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P2)).Count);
        Assert.Equal(P1, Assert.Single(game.State.Chain).Controller);
    }

    [Fact]
    public void A_trigger_waits_until_the_resolution_that_caused_it_has_finished()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", KillThenDraw), ("unit-3", DrawOnDeath)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Equal(P2, Assert.Single(game.State.Chain).Controller);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void A_keyword_trigger_shows_its_keyword_line_on_the_chain()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soaring-scout"));
        var scout = game.Put("soaring-scout", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.SubmitManual(P1, new ManualDamage(scout, 1)).Accepted);

        Assert.StartsWith("[Deathknell]", Assert.Single(game.State.Chain).Text);
    }
}
