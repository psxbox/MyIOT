namespace MyIOT.Api.Services;

/// <summary>
/// Cache service for storing and retrieving latest telemetry values.
/// </summary>
public interface ICacheService
{
    Task SetLatestTelemetryAsync(Guid deviceId, string key, double value, DateTime timestamp);
    Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetLatestTelemetryAsync(Guid deviceId);
    Task RemoveDeviceCacheAsync(Guid deviceId);
}
