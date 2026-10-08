using CromoBound.Engine.Random;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

public class GameStateTests
{
    private static readonly PlayerId P1 = new(0);
    private static readonly PlayerId P2 = new(1);

    private static GameState NewState(ulong seed = 1) => new(2, new SeededRandom(seed));

    [Fact]
    public void Create_places_the_object_and_lists_it()
    {
        var state = NewState();

        var id = state.Create("unit-2", "p-unit-2", P1, Place.Hand(P1));

        Assert.Equal(new[] { id }, state.At(Place.Hand(P1)));
        Assert.Equal(P1, state[id].Controller);
        Assert.Equal(Place.Hand(P1), state[id].Place);
    }

    [Fact]
    public void Ordered_piles_keep_creation_order_and_take_top_or_bottom()
    {
        var state = NewState();
        var a = state.Create("a", "p-a", P1, Place.MainDeck(P1));
        var b = state.Create("b", "p-b", P1, Place.MainDeck(P1));
        var hand = state.Create("c", "p-c", P1, Place.Hand(P1));

        var top = state.Move(hand, Place.MainDeck(P1), DeckPosition.Top)!.Value;
        var bottomCard = state.Create("d", "p-d", P1, Place.Hand(P1));
        var bottom = state.Move(bottomCard, Place.MainDeck(P1), DeckPosition.Bottom)!.Value;

        Assert.Equal(new[] { top, a, b, bottom }, state.At(Place.MainDeck(P1)));
    }

    [Fact]
    public void Moving_between_board_locations_keeps_the_object()
    {
        var state = NewState();
        var unit = state.Create("unit-2", "p-unit-2", P1, Place.Base(P1));
        state[unit].Damage = 1;

        var after = state.Move(unit, Place.Battlefield(0));

        Assert.Equal(unit, after);
        Assert.Equal(1, state[unit].Damage);
        Assert.Equal(Place.Battlefield(0), state[unit].Place);
    }

    [Fact]
    public void Moving_to_a_non_board_zone_makes_a_new_object()
    {
        var state = NewState();
        var unit = state.Create("unit-2", "p-unit-2", P1, Place.Base(P2));
        state[unit].Controller = P2;
        state[unit].Damage = 1;
        state[unit].Exhausted = true;
        state[unit].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));

        var after = state.Move(unit, Place.Hand(P1))!.Value;

        Assert.NotEqual(unit, after);
        Assert.False(state.Exists(unit));
        var card = state[after];
        Assert.Equal(0, card.Damage);
        Assert.False(card.Exhausted);
        Assert.Empty(card.Modifiers);
        Assert.Equal(P1, card.Controller);
    }

    [Fact]
    public void Leaving_a_non_board_zone_also_makes_a_new_object()
    {
        var state = NewState();
        var card = state.Create("unit-2", "p-unit-2", P1, Place.Hand(P1));

        var after = state.Move(card, Place.Base(P1));

        Assert.NotEqual(card, after);
    }

    [Fact]
    public void Token_leaving_the_board_ceases_to_exist()
    {
        var state = NewState();
        var token = state.Create("token-recruit", null, P1, Place.Base(P1), isToken: true);
        var toChain = state.Create("token-recruit", null, P1, Place.Base(P1), isToken: true);

        var gone = state.Move(token, Place.Trash(P1));
        var stillThere = state.Move(toChain, Place.Chain);

        Assert.Null(gone);
        Assert.False(state.Exists(token));
        Assert.Empty(state.At(Place.Trash(P1)));
        Assert.NotNull(stillThere);
    }

    [Fact]
    public void Cards_always_go_to_their_owners_piles()
    {
        var state = NewState();
        var stolen = state.Create("unit-2", "p-unit-2", P1, Place.Base(P2));
        state[stolen].Controller = P2;

        var after = state.Move(stolen, Place.Trash(P2))!.Value;

        Assert.Equal(Place.Trash(P1), state[after].Place);
        Assert.Empty(state.At(Place.Trash(P2)));
    }

    [Fact]
    public void Shuffle_is_deterministic_for_a_seed()
    {
        var first = NewState(seed: 9);
        var second = NewState(seed: 9);
        foreach (var state in new[] { first, second })
            for (var i = 0; i < 10; i++) state.Create($"c{i}", null, P1, Place.MainDeck(P1));

        first.Shuffle(Place.MainDeck(P1));
        second.Shuffle(Place.MainDeck(P1));

        Assert.Equal(first.At(Place.MainDeck(P1)), second.At(Place.MainDeck(P1)));
        Assert.NotEqual(Enumerable.Range(1, 10).Select(i => new ObjectId(i)), first.At(Place.MainDeck(P1)));
    }

    [Fact]
    public void Objects_are_enumerated_in_id_order_and_opponent_is_the_other_player()
    {
        var state = NewState();
        var b = state.Create("b", null, P1, Place.Hand(P1));
        var a = state.Create("a", null, P1, Place.Base(P1));
        state.Move(b, Place.Trash(P1));

        Assert.Equal(state.Objects.Select(o => o.Id.Value).Order(), state.Objects.Select(o => o.Id.Value));
        Assert.Contains(state.Objects, o => o.Id == a);
        Assert.Equal(P2, state.Opponent(P1));
        Assert.Equal(P1, state.Opponent(P2));
    }

    [Fact]
    public void Turn_state_tracks_scored_battlefields_per_player()
    {
        var state = NewState();

        state.Turn.MarkScored(P1, 1);

        Assert.True(state.Turn.HasScored(P1, 1));
        Assert.False(state.Turn.HasScored(P1, 0));
        Assert.False(state.Turn.HasScored(P2, 1));
    }
}
