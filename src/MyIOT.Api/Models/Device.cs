namespace MyIOT.Api.Models;

/// <summary>
/// Device entity stored in PostgreSQL.
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique token used for device authentication (MQTT username or HTTP login).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<DeviceAttribute> Attributes { get; set; } = new List<DeviceAttribute>();
    public ICollection<TelemetryRecord> TelemetryRecords { get; set; } = new List<TelemetryRecord>();
}
