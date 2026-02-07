namespace MyIOT.Shared.Responses;

/// <summary>
/// Returned after device registration.
/// </summary>
public class DeviceCreateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
