using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MyIOT.Api.Mqtt;
using MyIOT.Api.Services;
using MyIOT.Shared.Constants;
using MyIOT.Shared.Models;

namespace MyIOT.Tests.Mqtt;

public class MqttMessageHandlerTests
{
    private readonly Mock<ITelemetryService> _telemetryServiceMock;
    private readonly Mock<IAttributeService> _attributeServiceMock;
    private readonly MqttMessageHandler _sut;

    public MqttMessageHandlerTests()
    {
        _telemetryServiceMock = new Mock<ITelemetryService>();
        _attributeServiceMock = new Mock<IAttributeService>();

        // Build a mock IServiceScopeFactory that returns our mocked services
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(ITelemetryService)))
            .Returns(_telemetryServiceMock.Object);
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IAttributeService)))
            .Returns(_attributeServiceMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(scope.Object);

        var logger = new Mock<ILogger<MqttMessageHandler>>();

        _sut = new MqttMessageHandler(scopeFactory.Object, logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TelemetryTopic_ShouldCallTelemetryService()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, double>
        {
            ["temperature"] = 23.5,
            ["humidity"] = 55.0
        });

        // Act
        await _sut.HandleAsync(MqttTopics.Telemetry, payload, deviceId);

        // Assert
        _telemetryServiceMock.Verify(
            s => s.SaveAsync(deviceId, It.Is<Dictionary<string, double>>(d =>
                d["temperature"] == 23.5 && d["humidity"] == 55.0)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AttributesTopic_ShouldCallAttributeService()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{\"firmware\":\"2.0.0\",\"model\":\"SensorY\"}");

        // Act
        await _sut.HandleAsync(MqttTopics.Attributes, payload, deviceId);

        // Assert
        _attributeServiceMock.Verify(
            s => s.SaveAsync(deviceId, It.IsAny<Dictionary<string, object>>(), AttributeScope.Client),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownTopic_ShouldNotCallAnyService()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{}");

        // Act
        await _sut.HandleAsync("v1/devices/me/unknown", payload, deviceId);

        // Assert
        _telemetryServiceMock.Verify(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<string, double>>()), Times.Never);
        _attributeServiceMock.Verify(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<AttributeScope>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmptyTelemetry_ShouldNotCallService()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{}");

        // Act
        await _sut.HandleAsync(MqttTopics.Telemetry, payload, deviceId);

        // Assert
        _telemetryServiceMock.Verify(
            s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<string, double>>()),
            Times.Never);
    }
}
