namespace MyIOT.Shared.Requests;

/// <summary>
/// Request to register a new device.
/// </summary>
public class DeviceCreateRequest
{
    public string Name { get; set; } = string.Empty;
}
