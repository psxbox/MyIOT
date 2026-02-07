using System.Security.Claims;
using MyIOT.Api.Services;
using MyIOT.Shared.Requests;

namespace MyIOT.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static RouteGroupBuilder MapTelemetryEndpoints(this RouteGroupBuilder group)
    {
        // Device sends telemetry (authenticated)
        group.MapPost("/telemetry", async (TelemetryRequest request, ITelemetryService telemetryService,
            ClaimsPrincipal user) =>
        {
            var deviceId = GetDeviceId(user);
            if (deviceId is null)
                return Results.Unauthorized();

            if (request.Values.Count == 0)
                return Results.BadRequest("Telemetry values cannot be empty.");

            await telemetryService.SaveAsync(deviceId.Value, request.Values);
            return Results.Ok(new { message = "Telemetry saved", count = request.Values.Count });
        })
        .WithName("SendTelemetry")
        .WithOpenApi()
        .RequireAuthorization();

        // Get latest telemetry for a device
        group.MapGet("/devices/{id:guid}/telemetry/latest", async (Guid id, ITelemetryService telemetryService) =>
        {
            var latest = await telemetryService.GetLatestAsync(id);
            return Results.Ok(latest);
        })
        .WithName("GetLatestTelemetry")
        .WithOpenApi()
        .RequireAuthorization();

        // Get telemetry history for a device
        group.MapGet("/devices/{id:guid}/telemetry", async (
            Guid id,
            string key,
            DateTime from,
            DateTime to,
            ITelemetryService telemetryService) =>
        {
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest("Query parameter 'key' is required.");

            var history = await telemetryService.GetHistoryAsync(id, key, from, to);
            return Results.Ok(history);
        })
        .WithName("GetTelemetryHistory")
        .WithOpenApi()
        .RequireAuthorization();

        return group;
    }

    private static Guid? GetDeviceId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("device_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
