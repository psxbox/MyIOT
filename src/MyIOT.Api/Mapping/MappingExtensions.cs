using MyIOT.Api.Models;
using MyIOT.Shared.Models;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Mapping;

/// <summary>
/// Extension methods for mapping Entity models to Shared DTOs.
/// </summary>
public static class MappingExtensions
{
    public static DeviceDto ToDto(this Device device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        CreatedAt = device.CreatedAt
    };

    public static AttributeResponse ToResponse(this DeviceAttribute attribute) => new()
    {
        Key = attribute.Key,
        Value = attribute.Value,
        Scope = attribute.Scope,
        UpdatedAt = attribute.UpdatedAt
    };

    public static TelemetryLatestResponse ToLatestResponse(this TelemetryRecord record) => new()
    {
        Key = record.Key,
        Value = record.Value,
        Timestamp = record.Timestamp
    };

    public static TelemetryDataPoint ToDataPoint(this TelemetryRecord record) => new()
    {
        Value = record.Value,
        Timestamp = record.Timestamp
    };
}
