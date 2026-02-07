using MyIOT.Shared.Models;

namespace MyIOT.Shared.Responses;

/// <summary>
/// Single device attribute key-value entry.
/// </summary>
public class AttributeResponse
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public AttributeScope Scope { get; set; }
    public DateTime UpdatedAt { get; set; }
}
