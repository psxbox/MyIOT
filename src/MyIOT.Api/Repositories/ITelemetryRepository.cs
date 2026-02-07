using MyIOT.Api.Models;

namespace MyIOT.Api.Repositories;

public interface ITelemetryRepository
{
    Task InsertAsync(TelemetryRecord record);
    Task InsertBatchAsync(IEnumerable<TelemetryRecord> records);
    Task<List<TelemetryRecord>> GetLatestAsync(Guid deviceId);
    Task<List<TelemetryRecord>> GetHistoryAsync(Guid deviceId, string key, DateTime from, DateTime to);
}
