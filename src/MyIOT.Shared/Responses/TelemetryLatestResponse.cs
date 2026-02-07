namespace MyIOT.Shared.Responses;

/// <summary>
/// Single latest telemetry data point.
/// </summary>
public class TelemetryLatestResponse
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
