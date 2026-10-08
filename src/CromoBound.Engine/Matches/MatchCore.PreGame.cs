using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Matches;

/// <summary>The pre-game steps of spec §9.3: battlefields, play order, sideboarding, setup, mulligan.</summary>
internal sealed partial class MatchCore
{
    internal void BeginGame()
    {
        GameNumber++;
        Game = null;
        PlayStartIndex = -1;
        Array.Clear(Picks);
        Stage = MatchStage.PickBattlefields;
        Emit(new GameStarted(GameNumber));
        Emit(new LegendsRevealed([.. Decks.Select(d => d.Legend)]));
        if (Setup.Format == MatchFormat.Bo1)
        {
            foreach (var player in Players) Picks[player.Index] = Available[player.Index][Rng.NextInt(Available[player.Index].Count)];
            BattlefieldsPicked();
            return;
        }
        AskPicks();
    }

    /// <summary>Bo3: each player picks an unused battlefield; a single remaining option is picked automatically.</summary>
    private void AskPicks()
    {
        foreach (var player in Players)
            if (Picks[player.Index] is null && Available[player.Index].Count == 1) Picks[player.Index] = Available[player.Index][0];
        var waiting = Players.Where(p => Picks[p.Index] is null).ToList();
        if (waiting.Count == 0)
        {
            BattlefieldsPicked();
            return;
        }
        Ask(new PickBattlefieldDecision(waiting, [.. waiting.Select(p => new BattlefieldChoice(p, [.. Available[p.Index]]))]), (player, action) =>
        {
            if (action is not PickBattlefield pick || !Available[player.Index].Contains(pick.Printing))
                return Reject(RejectionCode.UnexpectedAction, "Pick one of your unused battlefields.");
            Picks[player.Index] = pick.Printing;
            AskPicks();
            return null;
        });
    }

    private void BattlefieldsPicked()
    {
        Emit(new BattlefieldsChosen([.. Picks.Select(p => p!)]));
        Stage = MatchStage.PlayOrder;
        var chooser = GameNumber == 1 ? RollOff() : LastLoser!.Value;
        Ask(new ChoosePlayOrderDecision(chooser), (_, action) =>
        {
            if (action is not ChoosePlayOrder order) return Reject(RejectionCode.UnexpectedAction, "Choose to play first or last.");
            First = order.First ? chooser : Opponent(chooser);
            Emit(new PlayOrderChosen(chooser, First));
            AfterPlayOrder();
            return null;
        });
    }

    /// <summary>Each player rolls a d20 until the results differ; the higher roll chooses play order.</summary>
    private PlayerId RollOff()
    {
        while (true)
        {
            var rolls = Players.Select(p => (Player: p, Value: Rng.RollD20())).ToList();
            foreach (var (player, value) in rolls) Emit(new D20Rolled(player, value));
            if (rolls[0].Value != rolls[1].Value) return rolls.MaxBy(r => r.Value).Player;
        }
    }

    /// <summary>Bo1 sideboards before its only game; Bo3 from game 2 on.</summary>
    private void AfterPlayOrder()
    {
        if (Setup.Format == MatchFormat.Bo3 && GameNumber == 1)
        {
            SetUpGame();
            return;
        }
        Stage = MatchStage.Sideboarding;
        AskSideboards([.. Players]);
    }

    private void AskSideboards(List<PlayerId> waiting)
    {
        if (waiting.Count == 0)
        {
            SetUpGame();
            return;
        }
        Ask(new SideboardDecision(waiting, [.. waiting.Select(Choice)]), (player, action) =>
        {
            if (action is not SubmitSideboard submit) return Reject(RejectionCode.UnexpectedAction, "Submit your sideboard swaps, or none.");
            var current = Decks[player.Index];
            var deck = Sideboarding.Apply(current, submit, out var error);
            if (deck is null) return Reject(RejectionCode.InvalidSideboard, error!);
            var report = DeckValidator.Validate(deck, Db);
            if (!report.IsLegal) return Reject(RejectionCode.InvalidSideboard, string.Join(" ", report.Issues.Select(i => i.Message)));
            Decks[player.Index] = deck;
            Emit(new SideboardChanged(player, submit.Swaps.Count, deck.Champion != current.Champion));
            AskSideboards([.. waiting.Where(p => p != player)]);
            return null;
        });
    }

    /// <summary>The player's deck as it stands, with the cards that may become the Chosen Champion (the current one and any champion unit in the main deck or sideboard that shares a tag with the legend).</summary>
    private SideboardChoice Choice(PlayerId player)
    {
        var deck = Decks[player.Index];
        var legendTags = Db.Cards[Db.Printings[deck.Legend].CardId].Tags;
        var candidates = deck.Main.Concat(deck.Sideboard).Select(e => e.Printing)
            .Where(printing => Db.Cards[Db.Printings[printing].CardId] is { Type: CardType.Unit, Supertype: Supertype.Champion } card
                && card.Tags.Intersect(legendTags, StringComparer.Ordinal).Any())
            .Append(deck.Champion)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return new SideboardChoice(player, deck.Main, deck.Sideboard, deck.Champion, [.. candidates]);
    }

    /// <summary>Creates the game's cards from the current decks and picked battlefields, shuffles, and deals 4 each (CR 110-116).</summary>
    private void SetUpGame()
    {
        var state = new GameState(Players.Count, Rng);
        Game = new Game(state, Db);
        foreach (var player in Players)
        {
            var deck = Decks[player.Index];
            Create(state, deck.Legend, player, Place.LegendZone(player));
            Create(state, deck.Champion, player, Place.ChampionZone(player));
            foreach (var entry in deck.Main)
                for (var i = 0; i < entry.Count; i++) Create(state, entry.Printing, player, Place.MainDeck(player));
            foreach (var entry in deck.Runes)
                for (var i = 0; i < entry.Count; i++) Create(state, entry.Printing, player, Place.RuneDeck(player));
            var battlefield = Create(state, Picks[player.Index]!, player, Place.BattlefieldCard(player.Index));
            state.Battlefields.Add(new BattlefieldState(player.Index, battlefield));
            state.Shuffle(Place.MainDeck(player));
            state.Shuffle(Place.RuneDeck(player));
        }
        Stage = MatchStage.Mulligan;
        Game.Draw(First, 4);
        Game.Draw(Opponent(First), 4);
        AskMulligan(First);
    }

    private ObjectId Create(GameState state, string printing, PlayerId owner, Place place) =>
        state.Create(Db.Printings[printing].CardId, printing, owner, place);

    /// <summary>CR 117, in turn order: set aside up to 2, draw that many, then recycle the set-aside cards to the bottom in random order.</summary>
    private void AskMulligan(PlayerId player)
    {
        var state = Game!.State;
        Ask(new MulliganDecision(player, [.. state.At(Place.Hand(player))]), (_, action) =>
        {
            if (action is not Mulligan mulligan) return Reject(RejectionCode.UnexpectedAction, "Choose up to 2 cards to set aside, or none.");
            var hand = state.At(Place.Hand(player));
            if (mulligan.SetAside.Count > 2 || mulligan.SetAside.Distinct().Count() != mulligan.SetAside.Count
                || mulligan.SetAside.Any(id => !hand.Contains(id)))
                return Reject(RejectionCode.UnexpectedAction, "Set aside at most 2 different cards from your hand.");
            Game.Draw(player, mulligan.SetAside.Count);
            var aside = mulligan.SetAside.ToList();
            Rng.Shuffle(aside);
            foreach (var id in aside) Game.MoveCard(id, Place.MainDeck(player), DeckPosition.Bottom);
            Emit(new MulliganTaken(player, aside.Count));
            if (player == First) AskMulligan(Opponent(First));
            else StartPlay();
            return null;
        });
    }

    private void StartPlay()
    {
        Stage = MatchStage.Playing;
        PullGameEvents();
        _newEvents.AddRange(Game!.Start(First));
        AfterGameChange();
    }
}
