namespace MyIOT.Shared.Requests;

/// <summary>
/// Telemetry payload sent by a device.
/// Keys are telemetry names, values are numeric readings.
/// Example: { "temperature": 25.3, "humidity": 60.0 }
/// </summary>
public class TelemetryRequest
{
    public Dictionary<string, double> Values { get; set; } = new();
}
