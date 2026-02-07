namespace MyIOT.Shared.Models;

/// <summary>
/// Public device information (safe to share with UI).
/// </summary>
public class DeviceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
