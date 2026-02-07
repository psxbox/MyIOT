using System.Security.Cryptography;
using MyIOT.Api.Auth;
using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Shared.Requests;
using MyIOT.Shared.Responses;

namespace MyIOT.Api.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDeviceRepository deviceRepository,
        JwtTokenService jwtTokenService,
        ILogger<DeviceService> logger)
    {
        _deviceRepository = deviceRepository;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<DeviceCreateResponse> CreateDeviceAsync(DeviceCreateRequest request)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AccessToken = GenerateAccessToken(),
            CreatedAt = DateTime.UtcNow
        };

        await _deviceRepository.CreateAsync(device);
        _logger.LogInformation("Device created: {DeviceId} ({Name})", device.Id, device.Name);

        return new DeviceCreateResponse
        {
            Id = device.Id,
            Name = device.Name,
            AccessToken = device.AccessToken
        };
    }

    public async Task<DeviceLoginResponse?> AuthenticateAsync(DeviceLoginRequest request)
    {
        var device = await _deviceRepository.GetByAccessTokenAsync(request.AccessToken);
        if (device is null)
        {
            _logger.LogWarning("Authentication failed: invalid access token");
            return null;
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(device.Id, device.Name);

        _logger.LogInformation("Device authenticated: {DeviceId} ({Name})", device.Id, device.Name);

        return new DeviceLoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    public async Task<Device?> GetByIdAsync(Guid id)
    {
        return await _deviceRepository.GetByIdAsync(id);
    }

    public async Task<Device?> GetByAccessTokenAsync(string accessToken)
    {
        return await _deviceRepository.GetByAccessTokenAsync(accessToken);
    }

    public async Task<List<Device>> GetAllAsync()
    {
        return await _deviceRepository.GetAllAsync();
    }

    /// <summary>
    /// Generates a cryptographically secure random access token (20 bytes, Base64url).
    /// </summary>
    private static string GenerateAccessToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
