using MyIOT.Shared.Models;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public interface IAttributeService
{
    Task SaveAsync(Guid deviceId, Dictionary<string, object> values, AttributeScope scope);
    Task<List<AttributeResponse>> GetByDeviceAsync(Guid deviceId, AttributeScope? scope = null);
}
