using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>Open challenges (spec §6.2), kept in memory: a restart drops them. One gate serializes every change, so the rules can't be
/// raced.</summary>
internal sealed class Lobby(CardDatabase cards, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)
{
    public const string NoSuchPlayer = "There is no such player.";
    public const string NotYourself = "You can't challenge yourself.";
    public const string AlreadyChallenging = "You already have an open challenge.";
    public const string NoSuchChallenge = "There is no such challenge.";
    public const string IllegalDeck = "That deck isn't legal.";
    public const string NoDeck = "Send a deck.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OpenChallenge> _open = [];

    private sealed record OpenChallenge(Guid Id, MatchSeat From, MatchSeat To, MatchFormat Format, Deck Deck);

    /// <summary>The opponent must be another enabled user (found in any letter case); the deck must be legal; a challenger has one
    /// open challenge at a time.</summary>
    public async Task<HubReply> ChallengeAsync(MatchSeat me, string? opponent, MatchFormat format, Deck? deck)
    {
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (_open.Values.Any(c => c.From.UserId == me.UserId)) return HubReply.Fail(AlreadyChallenging);
            if (await FindPlayerAsync(opponent) is not { } them) return HubReply.Fail(NoSuchPlayer);
            if (them.UserId == me.UserId) return HubReply.Fail(NotYourself);
            var report = DeckValidator.Validate(deck, cards);
            if (!report.IsLegal) return new HubReply(null, IllegalDeck, report.Issues);
            var challenge = new OpenChallenge(Guid.NewGuid(), me, them, format, deck);
            _open.Add(challenge.Id, challenge);
            await hub.Clients.Group(GameHub.UserGroup(them.UserId)).ChallengeReceived(new ChallengeNotice(challenge.Id, me.UserName, format));
            return HubReply.Ok(challenge.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<HubReply> DeclineAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.To.UserId == me.UserId, ChallengeEnd.Declined);

    public Task<HubReply> CancelAsync(MatchSeat me, Guid challengeId) =>
        CloseAsync(challengeId, c => c.From.UserId == me.UserId, ChallengeEnd.Cancelled);

    /// <summary>A challenge the caller may not close reads as missing.</summary>
    private async Task<HubReply> CloseAsync(Guid challengeId, Func<OpenChallenge, bool> mayClose, ChallengeEnd reason)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_open.TryGetValue(challengeId, out var challenge) || !mayClose(challenge)) return HubReply.Fail(NoSuchChallenge);
            await RemoveAsync(challenge, reason);
            return HubReply.Done;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RemoveAsync(OpenChallenge challenge, ChallengeEnd reason)
    {
        _open.Remove(challenge.Id);
        await hub.Clients.Groups([GameHub.UserGroup(challenge.From.UserId), GameHub.UserGroup(challenge.To.UserId)])
            .ChallengeClosed(new ChallengeClosedNotice(challenge.Id, reason));
    }

    /// <summary>An enabled user by name in any letter case; a missing, disabled or malformed name is no one.</summary>
    private async Task<MatchSeat?> FindPlayerAsync(string? userName)
    {
        if (UserStore.UserNameProblem(userName) is not null) return null;
        using var scope = scopes.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserStore>().FindAsync(userName!);
        return user is { Disabled: false } ? new MatchSeat(user.Id, user.UserName) : null;
    }
}
