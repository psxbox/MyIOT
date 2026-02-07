namespace MyIOT.Shared.Models;

/// <summary>
/// Scope of device attributes (mirrors ThingsBoard attribute scopes).
/// </summary>
public enum AttributeScope
{
    /// <summary>Attributes reported by the device itself.</summary>
    Client = 0,

    /// <summary>Attributes set by the server / platform.</summary>
    Server = 1,

    /// <summary>Attributes shared between server and device.</summary>
    Shared = 2
}
