using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AttachmentTests
{
    /// <summary>P1 controls the first battlefield with a ready unit-2 there, and has gear-1 in their Base.</summary>
    private static (TestGame Test, Game Engine, ObjectId Unit, ObjectId Gear) Setup()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var gear = game.Put("gear-1", Place.Base(P1));
        return (game, game.Start(), unit, gear);
    }

    [Fact]
    public void Attached_gear_joins_its_unit_and_follows_it()
    {
        var (game, engine, unit, gear) = Setup();

        var attached = engine.SubmitManual(P1, new ManualAttach(gear, unit));

        Assert.True(attached.Accepted);
        Assert.Contains(attached.Events, e => e is Attached);
        Assert.Equal(unit, game.State[gear].AttachedTo);
        Assert.Equal(Place.Battlefield(0), game.State[gear].Place);
        engine.Accept(P1, new StandardMove { Units = [unit], Destination = Place.Base(P1) });
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Equal(unit, game.State[gear].AttachedTo);
    }

    [Fact]
    public void Gear_is_detached_and_recalled_when_its_unit_leaves_the_board()
    {
        var (game, engine, unit, gear) = Setup();
        Assert.True(engine.SubmitManual(P1, new ManualAttach(gear, unit)).Accepted);

        var killed = engine.SubmitManual(P1, new ManualDamage(unit, 2));

        Assert.False(game.State.Exists(unit));
        Assert.Null(game.State[gear].AttachedTo);
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Contains(killed.Events, e => e is Detached);
    }

    [Fact]
    public void Detached_gear_at_a_battlefield_goes_back_to_base()
    {
        var (game, engine, unit, gear) = Setup();
        Assert.True(engine.SubmitManual(P1, new ManualAttach(gear, unit)).Accepted);

        Assert.True(engine.SubmitManual(P1, new ManualDetach(gear)).Accepted);

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Equal(Place.Battlefield(0), game.State[unit].Place);
    }

    [Fact]
    public void Only_gear_in_play_attaches_to_a_unit_in_play()
    {
        var (game, engine, unit, gear) = Setup();
        var otherGear = game.Put("gear-1", Place.Base(P1));
        var inHand = game.Put("gear-1", Place.Hand(P1));

        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(unit, unit)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(gear, otherGear)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(inHand, unit)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualDetach(gear)).Rejection!.Code);
        Assert.Null(game.State[gear].AttachedTo);
    }

    [Fact]
    public void Attachment_actions_and_events_round_trip_through_json()
    {
        PlayerAction[] actions = [new ManualAttach(new ObjectId(3), new ObjectId(4)), new ManualDetach(new ObjectId(3))];
        GameEvent[] events = [new Attached(new ObjectId(3), new ObjectId(4)), new Detached(new ObjectId(3))];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);
            Assert.Contains($"\"{action.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        }
        foreach (var gameEvent in events)
        {
            var json = CromoJson.Serialize(gameEvent);
            Assert.Contains($"\"{gameEvent.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<GameEvent>(json)));
        }
    }
}
