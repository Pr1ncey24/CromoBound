namespace CromoBound.Server.Accounts;

/// <summary>The role claim values (spec §4.2): specific identifiers, never "Administrator" or "Player", and never written to a
/// response, a log or an error. An admin holds both, so admin implies player.</summary>
internal static class Roles
{
    public const string Seat = "cb-seat-2b8a57c4e019";
    public const string Steward = "cb-steward-6d1f0e93a74c";
}

/// <summary>Authorization policy names; internal, never written to a response either.</summary>
internal static class Policies
{
    public const string Seat = "seat";
    public const string Steward = "steward";
}
