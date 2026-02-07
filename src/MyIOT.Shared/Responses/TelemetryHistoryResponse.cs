namespace MyIOT.Shared.Responses;

/// <summary>
/// Telemetry history for a given key over a time range.
/// </summary>
public class TelemetryHistoryResponse
{
    public string Key { get; set; } = string.Empty;
    public List<TelemetryDataPoint> DataPoints { get; set; } = new();
}

public class TelemetryDataPoint
{
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
