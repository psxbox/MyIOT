namespace MyIOT.Shared.Requests;

/// <summary>
/// Device login using its access token.
/// </summary>
public class DeviceLoginRequest
{
    public string AccessToken { get; set; } = string.Empty;
}
