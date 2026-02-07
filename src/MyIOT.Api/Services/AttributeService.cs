using System.Text.Json;
using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Shared.Models;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public class AttributeService : IAttributeService
{
    private readonly IAttributeRepository _attributeRepository;
    private readonly ILogger<AttributeService> _logger;

    public AttributeService(
        IAttributeRepository attributeRepository,
        ILogger<AttributeService> logger)
    {
        _attributeRepository = attributeRepository;
        _logger = logger;
    }

    public async Task SaveAsync(Guid deviceId, Dictionary<string, object> values, AttributeScope scope)
    {
        var attributes = values.Select(kv => new DeviceAttribute
        {
            DeviceId = deviceId,
            Key = kv.Key,
            Value = SerializeValue(kv.Value),
            Scope = scope,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        await _attributeRepository.UpsertBatchAsync(attributes);

        _logger.LogInformation(
            "Attributes saved: {Count} keys for device {DeviceId} (scope={Scope})",
            attributes.Count, deviceId, scope);
    }

    public async Task<List<AttributeResponse>> GetByDeviceAsync(Guid deviceId, AttributeScope? scope = null)
    {
        var attributes = await _attributeRepository.GetByDeviceAsync(deviceId, scope);

        return attributes.Select(a => new AttributeResponse
        {
            Key = a.Key,
            Value = a.Value,
            Scope = a.Scope,
            UpdatedAt = a.UpdatedAt
        }).ToList();
    }

    private static string SerializeValue(object value)
    {
        if (value is JsonElement element)
            return element.GetRawText();

        return JsonSerializer.Serialize(value);
    }
}
