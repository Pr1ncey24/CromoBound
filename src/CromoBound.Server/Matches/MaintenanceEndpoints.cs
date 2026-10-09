using CromoBound.Server.Accounts;

namespace CromoBound.Server.Matches;

/// <summary>The maintenance switch for admins (spec §5.2, §6.7). Both routes answer with the switch and the number of running
/// matches, so a deploy can wait for them to finish.</summary>
internal static class MaintenanceEndpoints
{
    public const string SayOn = "Say whether maintenance is on.";

    public static void MapMaintenance(this IEndpointRouteBuilder app)
    {
        var maintenance = app.MapGroup("/api/admin/maintenance").RequireAuthorization(Policies.Steward);
        maintenance.MapGet("", (Maintenance state, MatchRegistry matches) => Status(state, matches));
        maintenance.MapPost("", Switch);
    }

    /// <summary>The body must say which.</summary>
    private static IResult Switch(MaintenanceRequest request, Maintenance state, MatchRegistry matches, ILogger<Maintenance> log)
    {
        if (request.On is not { } on) return Results.BadRequest(new ErrorResponse(SayOn));
        state.On = on;
        log.LogInformation("Maintenance is {State}.", on ? "on" : "off");
        return Results.Ok(Status(state, matches));
    }

    private static MaintenanceStatus Status(Maintenance state, MatchRegistry matches) => new(state.On, matches.Count);
}
