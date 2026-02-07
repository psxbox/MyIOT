using System.Text.Json;
using StackExchange.Redis;

namespace MyIOT.Api.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    private static string GetKey(Guid deviceId) => $"telemetry:latest:{deviceId}";

    public async Task SetLatestTelemetryAsync(Guid deviceId, string key, double value, DateTime timestamp)
    {
        var redisKey = GetKey(deviceId);
        var payload = JsonSerializer.Serialize(new { value, timestamp });

        await _redis.HashSetAsync(redisKey, key, payload);
        _logger.LogDebug("Redis SET {Key}.{Field} = {Value}", redisKey, key, value);
    }

    public async Task<Dictionary<string, (double Value, DateTime Timestamp)>> GetLatestTelemetryAsync(Guid deviceId)
    {
        var redisKey = GetKey(deviceId);
        var entries = await _redis.HashGetAllAsync(redisKey);

        var result = new Dictionary<string, (double, DateTime)>();

        foreach (var entry in entries)
        {
            try
            {
                var doc = JsonDocument.Parse(entry.Value.ToString());
                var value = doc.RootElement.GetProperty("value").GetDouble();
                var timestamp = doc.RootElement.GetProperty("timestamp").GetDateTime();
                result[entry.Name!] = (value, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Redis entry {Field}", entry.Name);
            }
        }

        return result;
    }

    public async Task RemoveDeviceCacheAsync(Guid deviceId)
    {
        var redisKey = GetKey(deviceId);
        await _redis.KeyDeleteAsync(redisKey);
    }
}
