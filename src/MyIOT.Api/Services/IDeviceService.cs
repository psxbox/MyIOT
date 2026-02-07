using MyIOT.Api.Models;
using MyIOT.Shared.Requests;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public interface IDeviceService
{
    Task<DeviceCreateResponse> CreateDeviceAsync(DeviceCreateRequest request);
    Task<DeviceLoginResponse?> AuthenticateAsync(DeviceLoginRequest request);
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device?> GetByAccessTokenAsync(string accessToken);
    Task<List<Device>> GetAllAsync();
}
