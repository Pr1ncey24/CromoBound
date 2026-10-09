using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>Who is signed in. Any 401 ends the session, once: the layout then goes to the login page (spec §5.1).</summary>
public sealed class SessionState
{
    public const string Ended = "Your session has ended.";

    public MeResponse? Me { get; private set; }

    public bool IsEnded { get; private set; }

    public event Action? SessionEnded;

    public void SignedIn(MeResponse me) => Me = me;

    public void End()
    {
        if (IsEnded) return;
        IsEnded = true;
        SessionEnded?.Invoke();
    }
}
