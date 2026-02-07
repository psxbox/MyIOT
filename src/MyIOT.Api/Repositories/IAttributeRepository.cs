using MyIOT.Api.Models;
using MyIOT.Shared.Models;

namespace MyIOT.Api.Repositories;

public interface IAttributeRepository
{
    Task UpsertAsync(DeviceAttribute attribute);
    Task UpsertBatchAsync(IEnumerable<DeviceAttribute> attributes);
    Task<List<DeviceAttribute>> GetByDeviceAsync(Guid deviceId, AttributeScope? scope = null);
}
