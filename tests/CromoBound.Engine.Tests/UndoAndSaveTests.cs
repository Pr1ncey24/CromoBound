using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class UndoAndSaveTests
{
    private static Match NewMatch() => Match.Create(TestDecks.Setup(MatchFormat.Bo1), EngineTestDb.Create()).Match!;

    private static PlayerId Other(PlayerId player) => new(1 - player.Index);

    /// <summary>To play, then the first player starts playing their first playable card (stopping at the payment).</summary>
    private static (Match Match, PlayerId Player) StartPlaying()
    {
        var match = NewMatch().ToPlay();
        var priority = match.Decision<PriorityDecision>();
        match.Accept(priority.Player, new PlayCard(priority.Playable[0]));
        return (match, priority.Player);
    }

    [Fact]
    public void Accepted_undo_equals_never_taking_the_action()
    {
        var (undone, player) = StartPlaying();
        var (reference, _) = StartPlaying();
        var pay = undone.Decision<PayCostDecision>().Suggested!;
        undone.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });

        undone.Accept(player, new RequestUndo());
        var confirm = undone.Decision<ConfirmUndoDecision>();
        Assert.Equal((Other(player), player), (confirm.Player, confirm.RequestedBy));
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<PayCostDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_declined_undo_changes_nothing()
    {
        var (match, player) = StartPlaying();
        var logBefore = match.ToRecord().Log.Count;

        match.Accept(player, new RequestUndo());
        match.Accept(Other(player), new AnswerUndo(false));

        Assert.IsType<PayCostDecision>(match.Pending);
        Assert.Equal(logBefore, match.ToRecord().Log.Count);
    }

    [Fact]
    public void A_declined_undo_leaves_no_trace_in_the_saved_match()
    {
        var (match, player) = StartPlaying();
        var pay = match.Decision<PayCostDecision>().Suggested!;
        match.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });
        match.Accept(player, new RequestUndo());
        match.Accept(Other(player), new AnswerUndo(false));
        match.Accept(player, new EndTurn());

        var loaded = Match.Load(match.ToRecord(), EngineTestDb.Create());

        Assert.Equal(CromoJson.Serialize(match.ViewFor(P1)), CromoJson.Serialize(loaded.ViewFor(P1)));
        Assert.Equal(CromoJson.Serialize(match.ViewFor(P2)), CromoJson.Serialize(loaded.ViewFor(P2)));
    }

    [Fact]
    public void Undo_needs_an_action_of_yours_since_play_began()
    {
        var match = NewMatch().ToMulligan();
        Assert.Equal(RejectionCode.UndoNotAllowed, match.Submit(new PlayerId(0), new RequestUndo()).Rejection!.Code);

        match.ToPlay();
        var opponent = Other(match.Decision<PriorityDecision>().Player);
        Assert.Equal(RejectionCode.UndoNotAllowed, match.Submit(opponent, new RequestUndo()).Rejection!.Code);
    }

    [Fact]
    public void While_an_undo_is_waiting_only_the_answer_or_a_concession_is_accepted()
    {
        var (match, player) = StartPlaying();
        match.Accept(player, new RequestUndo());

        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(player, new CancelPlay()).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(player, new AnswerUndo(true)).Rejection!.Code);
        match.Accept(player, new Concede());
        Assert.Equal(MatchStage.Over, match.Stage);
    }

    [Fact]
    public void A_saved_match_loads_into_the_same_state()
    {
        var (match, player) = StartPlaying();
        var pay = match.Decision<PayCostDecision>().Suggested!;
        match.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });
        match.Accept(player, new EndTurn());

        var json = CromoJson.Serialize(match.ToRecord());
        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(json), EngineTestDb.Create());

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
    }

    [Fact]
    public void Loading_a_record_from_another_version_is_refused()
    {
        var record = NewMatch().ToPlay().ToRecord();

        Assert.Throws<MatchVersionMismatchException>(() => Match.Load(record with { EngineVersion = "0.0.0+other" }, EngineTestDb.Create()));
        Assert.Throws<MatchVersionMismatchException>(() => Match.Load(record with { DataFingerprint = "other" }, EngineTestDb.Create()));
    }

    [Fact]
    public void A_log_entry_rejected_on_replay_fails_loading_and_names_it()
    {
        var record = NewMatch().ToPlay().ToRecord();
        var bad = record with { Log = [.. record.Log, new LoggedAction(new PlayerId(0), new ResolveDone())] };

        var error = Assert.Throws<InvalidDataException>(() => Match.Load(bad, EngineTestDb.Create()));

        Assert.Contains($"Log entry {record.Log.Count}", error.Message);
    }
}
