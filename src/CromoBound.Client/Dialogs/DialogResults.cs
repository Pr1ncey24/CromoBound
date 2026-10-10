using CromoBound.Client.Services;
using CromoBound.Engine.Matches;
using MudBlazor;

namespace CromoBound.Client.Dialogs;

public sealed record ChallengeChoice(MatchFormat Format, StoredDeck Deck);

public enum AcceptAnswer { Accept, Decline }

/// <summary>"Not now" is the dialog's cancel; Decline and Accept are answers.</summary>
public sealed record AcceptChoice(AcceptAnswer Answer, StoredDeck? Deck);

public enum DeckProblemsAnswer { GoToDecks, PickAnother }

public static class DialogDefaults
{
    public static DialogOptions Options { get; } = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
}
