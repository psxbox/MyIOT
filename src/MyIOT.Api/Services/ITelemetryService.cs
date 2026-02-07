using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public interface ITelemetryService
{
    Task SaveAsync(Guid deviceId, Dictionary<string, double> values);
    Task<List<TelemetryLatestResponse>> GetLatestAsync(Guid deviceId);
    Task<TelemetryHistoryResponse> GetHistoryAsync(Guid deviceId, string key, DateTime from, DateTime to);
}
