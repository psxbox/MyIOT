namespace MyIOT.Shared.Responses;

/// <summary>
/// Returned after successful device authentication.
/// </summary>
public class DeviceLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
