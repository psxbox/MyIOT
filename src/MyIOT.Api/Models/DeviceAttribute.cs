using MyIOT.Shared.Models;

namespace MyIOT.Api.Models;

/// <summary>
/// Device attribute entity (key-value with scope).
/// </summary>
public class DeviceAttribute
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// JSON-encoded attribute value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    public AttributeScope Scope { get; set; } = AttributeScope.Client;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Device Device { get; set; } = null!;
}
