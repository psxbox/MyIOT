namespace MyIOT.Shared.Models;

/// <summary>
/// Device info for dashboard display.
/// </summary>
public class DeviceInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastActivityAt { get; set; }
}
