using MyIOT.Api.Mapping;
using MyIOT.Api.Services;
using MyIOT.Shared.Requests;

namespace MyIOT.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/devices", async (DeviceCreateRequest request, IDeviceService deviceService) =>
        {
            var response = await deviceService.CreateDeviceAsync(request);
            return Results.Created($"/api/devices/{response.Id}", response);
        })
        .WithName("CreateDevice")
        .WithOpenApi()
        .AllowAnonymous(); // Device provisioning is open (in production, protect this)

        group.MapGet("/devices", async (IDeviceService deviceService) =>
        {
            var devices = await deviceService.GetAllAsync();
            return Results.Ok(devices.Select(d => d.ToDto()));
        })
        .WithName("ListDevices")
        .WithOpenApi();

        group.MapGet("/devices/{id:guid}", async (Guid id, IDeviceService deviceService) =>
        {
            var device = await deviceService.GetByIdAsync(id);
            if (device is null)
                return Results.NotFound();

            return Results.Ok(device.ToDto());
        })
        .WithName("GetDevice")
        .WithOpenApi();

        return group;
    }
}
