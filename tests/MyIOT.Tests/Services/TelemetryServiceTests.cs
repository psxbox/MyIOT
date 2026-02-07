using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MyIOT.Api.Models;
using MyIOT.Api.Repositories;
using MyIOT.Api.Services;

namespace MyIOT.Tests.Services;

public class TelemetryServiceTests
{
    private readonly Mock<ITelemetryRepository> _telemetryRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly TelemetryService _sut;

    public TelemetryServiceTests()
    {
        _telemetryRepoMock = new Mock<ITelemetryRepository>();
        _cacheMock = new Mock<ICacheService>();
        var logger = new Mock<ILogger<TelemetryService>>();

        _sut = new TelemetryService(_telemetryRepoMock.Object, _cacheMock.Object, logger.Object);
    }

    [Fact]
    public async Task SaveAsync_ShouldInsertRecordsAndUpdateCache()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var values = new Dictionary<string, double>
        {
            ["temperature"] = 25.5,
            ["humidity"] = 60.0
        };

        // Act
        await _sut.SaveAsync(deviceId, values);

        // Assert — DB insert
        _telemetryRepoMock.Verify(
            r => r.InsertBatchAsync(It.Is<IEnumerable<TelemetryRecord>>(
                records => records.Count() == 2)),
            Times.Once);

        // Assert — Redis cache updated for each key
        _cacheMock.Verify(
            c => c.SetLatestTelemetryAsync(deviceId, "temperature", 25.5, It.IsAny<DateTime>()),
            Times.Once);
        _cacheMock.Verify(
            c => c.SetLatestTelemetryAsync(deviceId, "humidity", 60.0, It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLatestAsync_WhenCacheHasData_ShouldReturnFromCache()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var cached = new Dictionary<string, (double Value, DateTime Timestamp)>
        {
            ["temperature"] = (25.5, DateTime.UtcNow)
        };

        _cacheMock
            .Setup(c => c.GetLatestTelemetryAsync(deviceId))
            .ReturnsAsync(cached);

        // Act
        var result = await _sut.GetLatestAsync(deviceId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Key.Should().Be("temperature");
        result[0].Value.Should().Be(25.5);

        // Should NOT hit the database
        _telemetryRepoMock.Verify(r => r.GetLatestAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetLatestAsync_WhenCacheEmpty_ShouldFallbackToDb()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        _cacheMock
            .Setup(c => c.GetLatestTelemetryAsync(deviceId))
            .ReturnsAsync(new Dictionary<string, (double, DateTime)>());

        _telemetryRepoMock
            .Setup(r => r.GetLatestAsync(deviceId))
            .ReturnsAsync(new List<TelemetryRecord>
            {
                new() { DeviceId = deviceId, Key = "pressure", Value = 1013.25, Timestamp = DateTime.UtcNow }
            });

        // Act
        var result = await _sut.GetLatestAsync(deviceId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Key.Should().Be("pressure");

        _telemetryRepoMock.Verify(r => r.GetLatestAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnDataPointsFromDb()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow;

        _telemetryRepoMock
            .Setup(r => r.GetHistoryAsync(deviceId, "temperature", from, to))
            .ReturnsAsync(new List<TelemetryRecord>
            {
                new() { DeviceId = deviceId, Key = "temperature", Value = 20.0, Timestamp = from.AddMinutes(10) },
                new() { DeviceId = deviceId, Key = "temperature", Value = 22.0, Timestamp = from.AddMinutes(30) },
                new() { DeviceId = deviceId, Key = "temperature", Value = 24.5, Timestamp = from.AddMinutes(50) }
            });

        // Act
        var result = await _sut.GetHistoryAsync(deviceId, "temperature", from, to);

        // Assert
        result.Key.Should().Be("temperature");
        result.DataPoints.Should().HaveCount(3);
        result.DataPoints[0].Value.Should().Be(20.0);
        result.DataPoints[2].Value.Should().Be(24.5);
    }
}
