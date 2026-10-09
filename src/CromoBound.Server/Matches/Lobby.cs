using System.Security.Cryptography;
using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>Open challenges (spec §6.2), kept in memory: a restart drops them. One gate serializes every change and every match start,
/// so the rules can't be raced. Players in a match can't make or receive challenges, and starting a match withdraws every other open
/// challenge of its players, so no open challenge ever involves a player who is in a match.</summary>
internal sealed class Lobby(CardDatabase cards, IMatchStore store, MatchRegistry matches, Maintenance maintenance,
    Presence presence, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)
{
    public const string InMaintenance = "The server is in maintenance.";
    public const string NoSuchPlayer = "There is no such player.";
    public const string NotYourself = "You can't challenge yourself.";
    public const string YouArePlaying = "You are already in a match.";
    public const string TheyArePlaying = "That player is in a match.";
    public const string AlreadyChallenging = "You already have an open challenge.";
    public const string NoSuchChallenge = "There is no such challenge.";
    public const string IllegalDeck = "That deck isn't legal.";
    public const string NoDeck = "Send a deck.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OpenChallenge> _open = [];

    private sealed record OpenChallenge(Guid Id, MatchSeat From, MatchSeat To, MatchFormat Format, Deck Deck);

    /// <summary>The opponent must be another enabled user (found in any letter case) who isn't in a match; the deck must be legal; a
    /// challenger has one open challenge at a time.</summary>
    public async Task<HubReply> ChallengeAsync(MatchSeat me, string? opponent, MatchFormat format, Deck? deck)
    {
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (maintenance.On) return HubReply.Fail(InMaintenance);
            if (matches.IsPlaying(me.UserId)) return HubReply.Fail(YouArePlaying);
            if (_open.Values.Any(c => c.From.UserId == me.UserId)) return HubReply.Fail(AlreadyChallenging);
            if (await FindPlayerAsync(opponent) is not { } them) return HubReply.Fail(NoSuchPlayer);
            if (them.UserId == me.UserId) return HubReply.Fail(NotYourself);
            if (matches.IsPlaying(them.UserId)) return HubReply.Fail(TheyArePlaying);
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

    /// <summary>Only the challenged player accepts, with a legal deck; an illegal one leaves the challenge open. A challenger who is no
    /// longer an enabled user withdraws the challenge. The match is created with a seed from a cryptographic generator and the
    /// challenger in seat 0 (the engine's roll-off still decides who plays first), saved, and opened. Every other open challenge of
    /// either player is withdrawn.</summary>
    public async Task<HubReply> AcceptAsync(MatchSeat me, Guid challengeId, Deck? deck)
    {
        if (deck is null) return HubReply.Fail(NoDeck);
        await _gate.WaitAsync();
        try
        {
            if (maintenance.On) return HubReply.Fail(InMaintenance);
            if (!_open.TryGetValue(challengeId, out var challenge) || challenge.To.UserId != me.UserId) return HubReply.Fail(NoSuchChallenge);
            if (await FindPlayerAsync(challenge.From.UserName) is not { } from || from.UserId != challenge.From.UserId)
            {
                await RemoveAsync(challenge, ChallengeEnd.Withdrawn);
                return HubReply.Fail(NoSuchChallenge);
            }
            var created = Match.Create(new MatchSetup(challenge.Format, challenge.Deck, deck, NewSeed()), cards);
            if (created.Match is not { } match) return new HubReply(null, IllegalDeck, created.Reports[1].Issues);
            var id = Guid.NewGuid();
            var record = match.ToRecord();
            await store.CreateAsync(id, challenge.From.UserId, challenge.To.UserId, record);
            var host = matches.Open(id, match, record, [challenge.From, challenge.To]);
            foreach (var other in _open.Values.Where(c => Involves(c, challenge.From) || Involves(c, challenge.To)).ToList())
                await RemoveAsync(other, other.Id == challengeId ? ChallengeEnd.Accepted : ChallengeEnd.Withdrawn);
            await host.StartAsync();
            await presence.AnnounceAsync(challenge.From.UserId);
            await presence.AnnounceAsync(challenge.To.UserId);
            return HubReply.Ok(id);
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

    /// <summary>The user's open challenges, made and received (spec §6.1).</summary>
    public async Task<IReadOnlyList<ChallengeInfo>> ChallengesOfAsync(int userId)
    {
        await _gate.WaitAsync();
        try
        {
            return [.. _open.Values.Where(c => c.From.UserId == userId || c.To.UserId == userId)
                .Select(c => new ChallengeInfo(c.Id, c.From.UserName, c.To.UserName, c.Format))];
        }
        finally
        {
            _gate.Release();
        }
    }

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

    private static bool Involves(OpenChallenge challenge, MatchSeat player) =>
        challenge.From.UserId == player.UserId || challenge.To.UserId == player.UserId;

    private static ulong NewSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));

    /// <summary>An enabled user by name in any letter case; a missing, disabled or malformed name is no one.</summary>
    private async Task<MatchSeat?> FindPlayerAsync(string? userName)
    {
        if (UserStore.UserNameProblem(userName) is not null) return null;
        using var scope = scopes.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserStore>().FindAsync(userName!);
        return user is { Disabled: false } ? new MatchSeat(user.Id, user.UserName) : null;
    }
}
