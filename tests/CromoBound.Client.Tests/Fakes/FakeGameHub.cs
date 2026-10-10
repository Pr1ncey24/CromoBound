using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>The hub, in memory. A test sets what each call answers, pushes notices with <see cref="Push"/>, and reads the calls made.</summary>
internal sealed class FakeGameHub : IGameHub
{
    private readonly List<IGameClient> _listeners = [];

    public HubState State { get; private set; } = HubState.Connecting;
    public event Action? StateChanged;
    public event Func<Task>? Connected;
    public event Func<Task>? Closed;

    public List<string> Calls { get; } = [];
    public int Starts { get; private set; }

    public LobbyReply? Lobby { get; set; } = new([], [], null, null, false);
    public MatchReply? Match { get; set; } = MatchReply.None;

    /// <summary>What the next challenge call answers, by method name ("Challenge", "Accept", "Decline", "Cancel"); Ok by default.</summary>
    public Dictionary<string, HubReply> Replies { get; } = [];

    /// <summary>When set, the challenge call waits for it before answering, so a test can look at the page while the call is in flight.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public SubmitReply Submitted { get; set; } = new(true, null, null);
    public List<PlayerAction> Actions { get; } = [];
    public List<Deck> Decks { get; } = [];

    public void Listen(IGameClient listener) => _listeners.Add(listener);

    public Task StartAsync()
    {
        Starts++;
        return Task.CompletedTask;
    }

    /// <summary>Moves to a state, firing <see cref="Connected"/> when it becomes connected, as the real connection does.</summary>
    public async Task SetStateAsync(HubState state)
    {
        State = state;
        StateChanged?.Invoke();
        if (state == HubState.Connected && Connected is { } connected) await connected();
    }

    public async Task CloseAsync()
    {
        State = HubState.Reconnecting;
        StateChanged?.Invoke();
        if (Closed is { } closed) await closed();
    }

    public Task Push(Func<IGameClient, Task> notice) => Task.WhenAll(_listeners.Select(notice));

    public Task<LobbyReply?> GetLobbyAsync()
    {
        Calls.Add("GetLobby");
        return Task.FromResult(Lobby);
    }

    public Task<MatchReply?> GetMatchAsync()
    {
        Calls.Add("GetMatch");
        return Task.FromResult(Match);
    }

    public async Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck)
    {
        Calls.Add($"Challenge {opponent} {format} {deck.Name}");
        Decks.Add(deck);
        if (Hold is { } hold) await hold.Task;
        return Reply("Challenge");
    }

    public Task<HubReply> AcceptAsync(Guid challengeId, Deck deck)
    {
        Calls.Add($"Accept {challengeId} {deck.Name}");
        Decks.Add(deck);
        return Task.FromResult(Reply("Accept"));
    }

    public Task<HubReply> DeclineAsync(Guid challengeId)
    {
        Calls.Add($"Decline {challengeId}");
        return Task.FromResult(Reply("Decline"));
    }

    public Task<HubReply> CancelAsync(Guid challengeId)
    {
        Calls.Add($"Cancel {challengeId}");
        return Task.FromResult(Reply("Cancel"));
    }

    public Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action)
    {
        Calls.Add($"Submit {matchId} {action.GetType().Name}");
        Actions.Add(action);
        return Task.FromResult(Submitted);
    }

    private HubReply Reply(string method) => Replies.Remove(method, out var reply) ? reply : HubReply.Ok(Guid.NewGuid());
}
