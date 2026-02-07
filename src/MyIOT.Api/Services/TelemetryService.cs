using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(
        ITelemetryRepository telemetryRepository,
        ICacheService cacheService,
        ILogger<TelemetryService> logger)
    {
        _telemetryRepository = telemetryRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task SaveAsync(Guid deviceId, Dictionary<string, double> values)
    {
        var now = DateTime.UtcNow;

        var records = values.Select(kv => new TelemetryRecord
        {
            DeviceId = deviceId,
            Key = kv.Key,
            Value = kv.Value,
            Timestamp = now
        }).ToList();

        // Insert into TimescaleDB
        await _telemetryRepository.InsertBatchAsync(records);

        // Update Redis cache for each key
        foreach (var record in records)
        {
            await _cacheService.SetLatestTelemetryAsync(
                deviceId, record.Key, record.Value, record.Timestamp);
        }

        _logger.LogInformation(
            "Telemetry saved: {Count} keys for device {DeviceId}",
            records.Count, deviceId);
    }

    public async Task<List<TelemetryLatestResponse>> GetLatestAsync(Guid deviceId)
    {
        // Try Redis first
        var cached = await _cacheService.GetLatestTelemetryAsync(deviceId);
        if (cached.Count > 0)
        {
            _logger.LogDebug("Latest telemetry served from Redis for {DeviceId}", deviceId);
            return cached.Select(kv => new TelemetryLatestResponse
            {
                Key = kv.Key,
                Value = kv.Value.Value,
                Timestamp = kv.Value.Timestamp
            }).ToList();
        }

        // Fallback to database
        _logger.LogDebug("Latest telemetry served from DB for {DeviceId}", deviceId);
        var records = await _telemetryRepository.GetLatestAsync(deviceId);
        return records.Select(r => new TelemetryLatestResponse
        {
            Key = r.Key,
            Value = r.Value,
            Timestamp = r.Timestamp
        }).ToList();
    }

    public async Task<TelemetryHistoryResponse> GetHistoryAsync(
        Guid deviceId, string key, DateTime from, DateTime to)
    {
        var records = await _telemetryRepository.GetHistoryAsync(deviceId, key, from, to);

        return new TelemetryHistoryResponse
        {
            Key = key,
            DataPoints = records.Select(r => new TelemetryDataPoint
            {
                Value = r.Value,
                Timestamp = r.Timestamp
            }).ToList()
        };
    }
}
