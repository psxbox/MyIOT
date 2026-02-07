using Microsoft.EntityFrameworkCore;
using MyIOT.Api.Data;
using MyIOT.Api.Models;

namespace MyIOT.Api.Repositories;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly AppDbContext _db;

    public TelemetryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task InsertAsync(TelemetryRecord record)
    {
        _db.TelemetryRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task InsertBatchAsync(IEnumerable<TelemetryRecord> records)
    {
        _db.TelemetryRecords.AddRange(records);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Gets the latest telemetry value for each key of a given device.
    /// Uses a raw SQL query optimized for TimescaleDB: DISTINCT ON.
    /// </summary>
    public async Task<List<TelemetryRecord>> GetLatestAsync(Guid deviceId)
    {
        return await _db.TelemetryRecords
            .FromSqlInterpolated($@"
                SELECT DISTINCT ON (key) device_id, key, timestamp, value
                FROM telemetry
                WHERE device_id = {deviceId}
                ORDER BY key, timestamp DESC")
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<TelemetryRecord>> GetHistoryAsync(
        Guid deviceId, string key, DateTime from, DateTime to)
    {
        return await _db.TelemetryRecords
            .AsNoTracking()
            .Where(t => t.DeviceId == deviceId
                     && t.Key == key
                     && t.Timestamp >= from
                     && t.Timestamp <= to)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();
    }
}
