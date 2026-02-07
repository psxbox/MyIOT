using MyIOT.Shared.Models;

namespace MyIOT.Shared.Requests;

/// <summary>
/// Attribute payload sent by a device or server.
/// Keys are attribute names, values are arbitrary JSON-compatible objects.
/// Example: { "firmware": "1.2.0", "model": "SensorX" }
/// </summary>
public class AttributeRequest
{
    public Dictionary<string, object> Values { get; set; } = new();
    public AttributeScope Scope { get; set; } = AttributeScope.Client;
}
