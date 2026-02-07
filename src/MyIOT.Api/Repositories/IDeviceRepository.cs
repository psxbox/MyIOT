using MyIOT.Api.Models;

namespace MyIOT.Api.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device?> GetByAccessTokenAsync(string accessToken);
    Task<List<Device>> GetAllAsync();
    Task<Device> CreateAsync(Device device);
    Task<bool> ExistsAsync(Guid id);
}
