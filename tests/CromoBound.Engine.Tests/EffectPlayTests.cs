using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EffectPlayTests
{
    private static readonly ZoneRef OpponentsTrash = new() { Zone = Zone.Trash, Owner = new PlayerRef { Kind = PlayerKind.Opponent } };

    /// <summary>Kharox's block: choose a unit in the opponent's trash, play it with the given cost mode, then draw 1.</summary>
    private static Step[] PickAndPlay(PlayCostMode? cost) =>
    [
        new ChooseCardStep { From = OpponentsTrash, Filter = new Filter { Type = CardType.Unit }, Store = "picked" },
        new PlayStep { Card = ObjectRef.Variable("picked"), Cost = cost, Store = "played" },
        new DrawStep { Amount = 1 },
    ];

    private static EffectContext Context() => new() { Controller = P1, SourceCardId = "spell" };

    [Fact]
    public void A_unit_from_the_opponents_trash_is_played_ignoring_its_cost()
    {
        var game = new TestGame();
        game.Put("unit-3", Place.Trash(P2));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = Context();

        var events = engine.RunNow(new ResolveEffectTask(context, PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));

        var unit = game.First(Place.Base(P1), "unit-3");
        Assert.Equal(P1, game.State[unit].Controller);
        Assert.Equal(P2, game.State[unit].Owner);
        Assert.True(game.State[unit].Exhausted);
        Assert.Equal(new[] { unit }, context.Vars["played"].Objects);
        Assert.Contains(events, e => e is CardPlayed { CardId: "unit-3", Controller.Index: 0 });
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_unit_played_ignoring_its_cost_can_still_be_accelerated()
    {
        var game = new TestGame();
        game.Put("accel-3", Place.Trash(P2));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        engine.RunNow(new ResolveEffectTask(Context(), PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));

        Assert.True(engine.Decision<PlayChoicesDecision>().AccelerateAvailable);
        engine.Accept(P1, new ChoosePlayOptions(Place.Base(P1), true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        Assert.False(game.State[game.First(Place.Base(P1), "accel-3")].Exhausted);
    }

    [Fact]
    public void A_play_from_an_effect_without_a_cost_mode_pays_the_cost()
    {
        var game = new TestGame();
        game.Put("unit-2", Place.Trash(P2));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        engine.RunNow(new ResolveEffectTask(Context(), PickAndPlay(null), _ => { }));

        Assert.Equal(2, engine.Decision<PayCostDecision>().Cost.Energy);
        engine.PayWithSuggestion(P1);
        Assert.Equal(P1, game.State[game.First(Place.Base(P1), "unit-2")].Controller);
    }

    [Fact]
    public void Cancelling_a_play_from_an_effect_puts_the_card_back_and_the_effect_goes_on()
    {
        var game = new TestGame();
        game.Put("accel-3", Place.Trash(P2));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = Context();
        engine.RunNow(new ResolveEffectTask(context, PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));
        engine.Decision<PlayChoicesDecision>();

        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "accel-3");
        Assert.False(context.Vars["played"].Happened);
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void A_stored_card_that_is_gone_or_already_in_play_is_not_played()
    {
        var game = new TestGame();
        var inPlay = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var context = Context();
        context.Vars["gone"] = new EffectVar([new ObjectId(999)], [], 1, true);
        context.Vars["here"] = new EffectVar([inPlay], [], 1, true);

        engine.RunNow(new ResolveEffectTask(context,
        [
            new PlayStep { Card = ObjectRef.Variable("gone"), Store = "first" },
            new PlayStep { Card = ObjectRef.Variable("here"), Store = "second" },
        ], _ => { }));

        Assert.False(context.Vars["first"].Happened);
        Assert.False(context.Vars["second"].Happened);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }
}
