using MyIOT.Api.Services;
using MyIOT.Shared.Requests;

namespace MyIOT.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/device/login", async (DeviceLoginRequest request, IDeviceService deviceService) =>
        {
            var response = await deviceService.AuthenticateAsync(request);
            if (response is null)
                return Results.Unauthorized();

            return Results.Ok(response);
        })
        .WithName("DeviceLogin")
        .AllowAnonymous();

        return group;
    }
}
