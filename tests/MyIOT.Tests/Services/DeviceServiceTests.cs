using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MyIOT.Api.Auth;
using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Api.Services;
using MyIOT.Shared.Requests;

namespace MyIOT.Tests.Services;

public class DeviceServiceTests
{
    private readonly Mock<IDeviceRepository> _deviceRepoMock;
    private readonly JwtTokenService _jwtTokenService;
    private readonly DeviceService _sut;

    public DeviceServiceTests()
    {
        _deviceRepoMock = new Mock<IDeviceRepository>();

        var jwtSettings = new JwtSettings
        {
            Secret = "TestSecretKeyThatIsLongEnoughForHmacSha256Algorithm!!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        };

        _jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        var logger = new Mock<ILogger<DeviceService>>();

        _sut = new DeviceService(_deviceRepoMock.Object, _jwtTokenService, logger.Object);
    }

    [Fact]
    public async Task CreateDeviceAsync_ShouldReturnDeviceWithAccessToken()
    {
        // Arrange
        var request = new DeviceCreateRequest { Name = "TestSensor" };

        _deviceRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<Device>()))
            .ReturnsAsync((Device d) => d);

        // Act
        var result = await _sut.CreateDeviceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("TestSensor");
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Id.Should().NotBeEmpty();

        _deviceRepoMock.Verify(r => r.CreateAsync(It.IsAny<Device>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidToken_ShouldReturnJwt()
    {
        // Arrange
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Sensor1",
            AccessToken = "valid-token",
            CreatedAt = DateTime.UtcNow
        };

        _deviceRepoMock
            .Setup(r => r.GetByAccessTokenAsync("valid-token"))
            .ReturnsAsync(device);

        var request = new DeviceLoginRequest { AccessToken = "valid-token" };

        // Act
        var result = await _sut.AuthenticateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        _deviceRepoMock
            .Setup(r => r.GetByAccessTokenAsync("invalid-token"))
            .ReturnsAsync((Device?)null);

        var request = new DeviceLoginRequest { AccessToken = "invalid-token" };

        // Act
        var result = await _sut.AuthenticateAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnDevice()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device { Id = deviceId, Name = "Sensor" };

        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(deviceId))
            .ReturnsAsync(device);

        // Act
        var result = await _sut.GetByIdAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(deviceId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        _deviceRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Device?)null);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }
}
