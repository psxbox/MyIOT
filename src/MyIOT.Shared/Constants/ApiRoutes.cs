namespace MyIOT.Shared.Constants;

/// <summary>
/// Centralized API route constants shared between backend and frontend.
/// </summary>
public static class ApiRoutes
{
    private const string Base = "/api";

    public static class Auth
    {
        public const string Login = $"{Base}/auth/device/login";
    }

    public static class Devices
    {
        public const string Create = $"{Base}/devices";
        public const string GetById = $"{Base}/devices/{{id}}";
        public const string List = $"{Base}/devices";
    }

    public static class Telemetry
    {
        public const string Send = $"{Base}/telemetry";
        public const string Latest = $"{Base}/devices/{{id}}/telemetry/latest";
        public const string History = $"{Base}/devices/{{id}}/telemetry";
    }

    public static class Attributes
    {
        public const string Send = $"{Base}/attributes";
        public const string Get = $"{Base}/devices/{{id}}/attributes";
    }
}
