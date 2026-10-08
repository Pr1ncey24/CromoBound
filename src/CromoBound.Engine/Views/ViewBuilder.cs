using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Views;

/// <summary>Builds a player's view from the match, leaving out everything they may not see.</summary>
internal static class ViewBuilder
{
    public static PlayerView Build(MatchCore core, PlayerId viewer)
    {
        var game = core.Game;
        var pending = core.Pending;
        return new PlayerView(
            viewer, core.Setup.Format, core.Stage, core.GameNumber, core.Winner,
            [.. MatchCore.Players.Select(p => Side(core, game, p, viewer))],
            game is null ? null : Turn(game),
            game is null ? [] : [.. game.State.Battlefields.Select(b => Battlefield(game, b, viewer))],
            game is null ? [] : [.. game.State.Chain.Select(i => ChainItem(game, i))],
            pending?.Players ?? Array.Empty<PlayerId>(),
            pending?.GetType().Name.Replace("Decision", ""),
            Decision(pending, viewer),
            [.. core.Events.Where(e => e.VisibleTo is null || e.VisibleTo == viewer)]);
    }

    private static PendingDecision? Decision(PendingDecision? pending, PlayerId viewer) => pending switch
    {
        PickBattlefieldDecision pick when pick.Players.Contains(viewer) => pick with { Choices = [.. pick.Choices.Where(c => c.Player == viewer)] },
        SideboardDecision sideboard when sideboard.Players.Contains(viewer) => sideboard with { Choices = [.. sideboard.Choices.Where(c => c.Player == viewer)] },
        { } decision when decision.Players.Contains(viewer) => decision,
        _ => null,
    };

    private static PlayerSideView Side(MatchCore core, Game? game, PlayerId player, PlayerId viewer)
    {
        var own = player == viewer;
        var deck = core.Decks[player.Index];
        var sideboard = own ? deck.Sideboard : null;
        var sideboardCount = deck.Sideboard.Sum(e => e.Count);
        if (game is null)
            return new PlayerSideView(player, 0, 0, core.Wins[player.Index], new PoolView(0, new SortedDictionary<Domain, int>(), 0),
                [], [], [], own ? [] : null, 0, 0, 0, [], [], sideboard, sideboardCount);

        var state = game.State;
        var side = state.Player(player);
        List<CardView> At(Place place) => [.. state.At(place).Select(id => Card(game, state[id]))];
        return new PlayerSideView(
            player, side.Points, side.Xp, core.Wins[player.Index],
            new PoolView(side.Pool.Energy, new SortedDictionary<Domain, int>(side.Pool.Power), side.Pool.UniversalPower),
            At(Place.LegendZone(player)), At(Place.ChampionZone(player)), At(Place.Base(player)),
            own ? At(Place.Hand(player)) : null, state.At(Place.Hand(player)).Count,
            state.At(Place.MainDeck(player)).Count, state.At(Place.RuneDeck(player)).Count,
            At(Place.Trash(player)), At(Place.Banishment(player)),
            sideboard, sideboardCount);
    }

    private static CardView Card(Game game, CardInstance card)
    {
        var effects = game.Effects.For(card.CardId);
        return new(
            card.Id, card.CardId, card.PrintingId, card.Owner, card.Controller,
            card.Exhausted, card.Stunned, card.Buffed, card.Empowered, card.Damage,
            game.Db.Cards[card.CardId].Type == CardType.Unit ? game.MightOf(card.Id) : null, card.Role,
            effects.Status, effects.ManualLines);
    }

    private static BattlefieldView Battlefield(Game game, BattlefieldState battlefield, PlayerId viewer)
    {
        var state = game.State;
        var facedown = state.At(Place.Facedown(battlefield.Index));
        var visible = facedown.Select(id => state[id]).FirstOrDefault(c => c.Controller == viewer);
        return new BattlefieldView(
            battlefield.Index, Card(game, state[battlefield.Card]), battlefield.Controller, battlefield.ContestedBy,
            [.. state.At(Place.Battlefield(battlefield.Index)).Select(id => Card(game, state[id]))],
            facedown.Count > 0, visible is null ? null : Card(game, visible));
    }

    private static ChainItemView ChainItem(Game game, ChainItem item) => new(
        item.Id, item.Kind, item.Controller, item.Status,
        item.Card is { } card && game.State.Exists(card) ? Card(game, game.State[card]) : null,
        item.SourceCardId, item.Text, item.Location, item.Accelerate,
        item.Effect is { } effect ? [.. effect.Targets.Select(t => (IReadOnlyList<ObjectId>)[.. t])] : []);

    private static TurnView Turn(Game game)
    {
        var turn = game.State.Turn;
        var showdown = game.State.Showdown;
        IReadOnlyList<IReadOnlyList<int>> scored =
        [
            .. game.State.Players.Select(p => game.State.Battlefields.Where(b => turn.HasScored(p.Id, b.Index)).Select(b => b.Index).ToList()),
        ];
        return new TurnView(
            turn.Number, turn.TurnPlayer, turn.Phase, turn.Step, turn.Priority, turn.Focus, game.State.Chain.Count > 0,
            showdown?.Battlefield, showdown?.IsCombat ?? false, showdown?.Attacker, showdown?.Defender, scored);
    }
}
