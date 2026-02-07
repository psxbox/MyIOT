using System.Security.Claims;
using MyIOT.Api.Services;
using MyIOT.Shared.Models;
using MyIOT.Shared.Requests;

namespace MyIOT.Api.Endpoints;

public static class AttributeEndpoints
{
    public static RouteGroupBuilder MapAttributeEndpoints(this RouteGroupBuilder group)
    {
        // Device sends attributes (authenticated)
        group.MapPost("/attributes", async (AttributeRequest request, IAttributeService attributeService,
            ClaimsPrincipal user) =>
        {
            var deviceId = GetDeviceId(user);
            if (deviceId is null)
                return Results.Unauthorized();

            if (request.Values.Count == 0)
                return Results.BadRequest("Attribute values cannot be empty.");

            await attributeService.SaveAsync(deviceId.Value, request.Values, request.Scope);
            return Results.Ok(new { message = "Attributes saved", count = request.Values.Count });
        })
        .WithName("SendAttributes")
        .RequireAuthorization();

        // Get attributes for a device
        group.MapGet("/devices/{id:guid}/attributes", async (
            Guid id,
            AttributeScope? scope,
            IAttributeService attributeService) =>
        {
            var attributes = await attributeService.GetByDeviceAsync(id, scope);
            return Results.Ok(attributes);
        })
        .WithName("GetAttributes")
        .RequireAuthorization();

        return group;
    }

    private static Guid? GetDeviceId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("device_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
