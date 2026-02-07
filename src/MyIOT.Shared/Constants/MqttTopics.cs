namespace MyIOT.Shared.Constants;

/// <summary>
/// MQTT topic constants for device communication.
/// </summary>
public static class MqttTopics
{
    /// <summary>Devices publish telemetry data to this topic.</summary>
    public const string Telemetry = "v1/devices/me/telemetry";

    /// <summary>Devices publish attribute updates to this topic.</summary>
    public const string Attributes = "v1/devices/me/attributes";

    /// <summary>Server publishes shared attributes to devices on this topic.</summary>
    public const string AttributesResponse = "v1/devices/me/attributes/response";

    /// <summary>Devices can request their attributes from the server.</summary>
    public const string AttributesRequest = "v1/devices/me/attributes/request";
}
