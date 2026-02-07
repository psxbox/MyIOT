using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Api.Services;
using MyIOT.Shared.Models;
using MyIOT.Shared.Responses;

namespace MyIOT.Tests.Services;

public class AttributeServiceTests
{
    private readonly Mock<IAttributeRepository> _attributeRepoMock;
    private readonly AttributeService _sut;

    public AttributeServiceTests()
    {
        _attributeRepoMock = new Mock<IAttributeRepository>();
        var logger = new Mock<ILogger<AttributeService>>();

        _sut = new AttributeService(_attributeRepoMock.Object, logger.Object);
    }

    [Fact]
    public async Task SaveAsync_ShouldCallUpsertBatchWithCorrectAttributes()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var values = new Dictionary<string, object>
        {
            ["firmware"] = "1.2.0",
            ["model"] = "SensorX"
        };

        // Act
        await _sut.SaveAsync(deviceId, values, AttributeScope.Client);

        // Assert
        _attributeRepoMock.Verify(
            r => r.UpsertBatchAsync(It.Is<IEnumerable<DeviceAttribute>>(attrs =>
                attrs.Count() == 2 &&
                attrs.All(a => a.DeviceId == deviceId && a.Scope == AttributeScope.Client))),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithServerScope_ShouldSetScopeCorrectly()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var values = new Dictionary<string, object>
        {
            ["location"] = "Building A"
        };

        // Act
        await _sut.SaveAsync(deviceId, values, AttributeScope.Server);

        // Assert
        _attributeRepoMock.Verify(
            r => r.UpsertBatchAsync(It.Is<IEnumerable<DeviceAttribute>>(attrs =>
                attrs.All(a => a.Scope == AttributeScope.Server))),
            Times.Once);
    }

    [Fact]
    public async Task GetByDeviceAsync_ShouldReturnMappedResponses()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var attributes = new List<DeviceAttribute>
        {
            new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Key = "firmware",
                Value = "\"1.2.0\"",
                Scope = AttributeScope.Client,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Key = "model",
                Value = "\"SensorX\"",
                Scope = AttributeScope.Client,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _attributeRepoMock
            .Setup(r => r.GetByDeviceAsync(deviceId, null))
            .ReturnsAsync(attributes);

        // Act
        var result = await _sut.GetByDeviceAsync(deviceId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Key.Should().Be("firmware");
        result[0].Value.Should().Be("\"1.2.0\"");
        result[1].Key.Should().Be("model");
    }

    [Fact]
    public async Task GetByDeviceAsync_WithScopeFilter_ShouldPassScopeToRepo()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        _attributeRepoMock
            .Setup(r => r.GetByDeviceAsync(deviceId, AttributeScope.Shared))
            .ReturnsAsync(new List<DeviceAttribute>());

        // Act
        var result = await _sut.GetByDeviceAsync(deviceId, AttributeScope.Shared);

        // Assert
        result.Should().BeEmpty();
        _attributeRepoMock.Verify(
            r => r.GetByDeviceAsync(deviceId, AttributeScope.Shared),
            Times.Once);
    }
}
