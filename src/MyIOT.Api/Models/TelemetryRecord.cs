namespace MyIOT.Api.Models;

/// <summary>
/// Single telemetry data point. Stored in a TimescaleDB hypertable.
/// Composite PK: (DeviceId, Key, Timestamp).
/// </summary>
public class TelemetryRecord
{
    public Guid DeviceId { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
