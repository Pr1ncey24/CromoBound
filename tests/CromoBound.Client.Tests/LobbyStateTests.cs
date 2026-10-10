using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;

namespace CromoBound.Client.Tests;

public class LobbyStateTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid M = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static (LobbyState State, List<string> Notices) Marco()
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse("marco", false));
        var state = new LobbyState(session);
        var notices = new List<string>();
        state.Notice += notices.Add;
        return (state, notices);
    }

    private static LobbyReply Reply(
        IReadOnlyList<PlayerPresence>? players = null, IReadOnlyList<ChallengeInfo>? challenges = null, Guid? match = null,
        MatchEndedNotice? ended = null, bool maintenance = false) =>
        new(players ?? [], challenges ?? [], match, ended, maintenance);

    [Fact]
    public void Loading_sorts_players_and_splits_the_challenges()
    {
        var (state, _) = Marco();

        state.Load(Reply(
            [new("sara", true, true), new("Giulia", true, false), new("luca", false, false)],
            [new(B, "marco", "luca", MatchFormat.Bo1), new(C, "sara", "marco", MatchFormat.Bo1), new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        Assert.True(state.Loaded);
        Assert.Equal(new[] { "Giulia", "luca", "sara" }, state.Players.Select(p => p.UserName));
        Assert.Equal(2, state.Online);
        Assert.Equal(new[] { "giulia", "sara" }, state.Received.Select(c => c.From));
        Assert.Equal(B, state.Sent?.ChallengeId);
    }

    [Fact]
    public void Loading_replaces_everything_and_keeps_the_match_reply()
    {
        var (state, _) = Marco();
        state.Load(Reply([new("giulia", true, false)], [new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        state.Load(Reply(match: M, maintenance: true), new MatchReply(M, null, "giulia"));

        Assert.Empty(state.Players);
        Assert.Empty(state.Received);
        Assert.Equal((M, "giulia", true), (state.MatchId, state.Opponent, state.Maintenance));
    }

    [Fact]
    public void An_abandoned_notice_is_taken_once()
    {
        var (state, _) = Marco();
        var ended = new MatchEndedNotice(M, MatchEndReason.Abandoned, [0, 0], null);

        state.Load(Reply(ended: ended), null);

        Assert.Equal(ended, state.TakeAbandoned());
        Assert.Null(state.TakeAbandoned());
    }

    [Fact]
    public async Task A_received_challenge_is_listed_and_announced()
    {
        var (state, notices) = Marco();
        state.Load(Reply(), null);

        await state.ChallengeReceived(new ChallengeNotice(A, "giulia", MatchFormat.Bo3));
        await state.ChallengeReceived(new ChallengeNotice(A, "giulia", MatchFormat.Bo3));

        Assert.Equal(new ChallengeInfo(A, "giulia", "marco", MatchFormat.Bo3), Assert.Single(state.Received));
        Assert.Equal(new[] { "giulia challenges you to a best of three." }, notices);
    }

    [Theory]
    [InlineData(ChallengeEnd.Declined, "luca declined your challenge.")]
    [InlineData(ChallengeEnd.Withdrawn, "Your challenge to luca was withdrawn.")]
    [InlineData(ChallengeEnd.Cancelled, "")]
    [InlineData(ChallengeEnd.Accepted, "")]
    public async Task Closing_your_sent_challenge_says_why(ChallengeEnd reason, string notice)
    {
        var (state, notices) = Marco();
        state.Load(Reply(challenges: [new(B, "marco", "luca", MatchFormat.Bo1)]), null);

        await state.ChallengeClosed(new ChallengeClosedNotice(B, reason));

        Assert.Null(state.Sent);
        Assert.Equal(notice, string.Join("|", notices));
    }

    [Theory]
    [InlineData(ChallengeEnd.Cancelled, "giulia cancelled their challenge.")]
    [InlineData(ChallengeEnd.Withdrawn, "giulia's challenge was withdrawn.")]
    [InlineData(ChallengeEnd.Declined, "")]
    [InlineData(ChallengeEnd.Accepted, "")]
    public async Task Closing_a_received_challenge_says_why(ChallengeEnd reason, string notice)
    {
        var (state, notices) = Marco();
        state.Load(Reply(challenges: [new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        await state.ChallengeClosed(new ChallengeClosedNotice(A, reason));

        Assert.Empty(state.Received);
        Assert.Equal(notice, string.Join("|", notices));
    }

    [Fact]
    public async Task Closing_an_unknown_challenge_changes_nothing()
    {
        var (state, notices) = Marco();
        state.Load(Reply(), null);
        var changes = 0;
        state.Changed += () => changes++;

        await state.ChallengeClosed(new ChallengeClosedNotice(C, ChallengeEnd.Declined));

        Assert.Equal((0, 0), (changes, notices.Count));
    }

    [Fact]
    public async Task A_started_match_is_tracked_and_announced_to_the_app()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);
        Guid? begun = null;
        state.MatchBegun += id => begun = id;

        await state.MatchStarted(new MatchStartedNotice(M, "giulia", new PlayerId(1)));

        Assert.Equal((M, "giulia", new PlayerId(1)), (state.MatchId, state.Opponent, state.Seat));
        Assert.Equal(M, begun);
    }

    [Fact]
    public async Task A_finished_match_is_kept_for_the_match_page_and_frees_the_player()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);
        await state.MatchStarted(new MatchStartedNotice(M, "giulia", new PlayerId(0)));
        var ended = new MatchEndedNotice(M, MatchEndReason.Finished, [2, 1], "marco");

        await state.MatchEnded(ended);

        Assert.Null(state.MatchId);
        Assert.Equal(ended, state.Ended);
    }

    [Fact]
    public async Task Presence_and_maintenance_notices_update_the_lobby()
    {
        var (state, _) = Marco();
        state.Load(Reply([new("luca", false, false), new("sara", true, false)]), null);
        var playerChanges = 0;
        state.PlayersChanged += () => playerChanges++;

        await state.PlayerChanged(new PlayerPresence("luca", true, false));
        await state.PlayerChanged(new PlayerPresence("chiara", true, false));
        await state.PlayerLeft(new PlayerLeftNotice("sara"));
        await state.MaintenanceChanged(new MaintenanceNotice(true));

        Assert.Equal(new[] { ("chiara", true), ("luca", true) }, state.Players.Select(p => (p.UserName, p.Online)));
        Assert.True(state.Maintenance);
        Assert.Equal(4, playerChanges);
    }

    [Fact]
    public void A_challenge_sent_from_this_tab_is_shown_until_it_is_gone()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);

        state.ChallengeSent(new ChallengeInfo(B, "marco", "luca", MatchFormat.Bo1));
        Assert.Equal(B, state.Sent?.ChallengeId);

        state.ChallengeGone(B);
        Assert.Null(state.Sent);
    }
}
