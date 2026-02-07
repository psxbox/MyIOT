using System.Text;
using System.Text.Json;
using MyIOT.Api.Services;
using MyIOT.Shared.Constants;
using MyIOT.Shared.Models;

namespace MyIOT.Api.Mqtt;

/// <summary>
/// Handles incoming MQTT messages, routes them to appropriate services.
/// </summary>
public class MqttMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MqttMessageHandler> _logger;

    public MqttMessageHandler(IServiceScopeFactory scopeFactory, ILogger<MqttMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Process an incoming MQTT publish message.
    /// </summary>
    /// <param name="topic">The MQTT topic.</param>
    /// <param name="payload">Raw UTF-8 payload bytes.</param>
    /// <param name="deviceId">The authenticated device's ID.</param>
    public async Task HandleAsync(string topic, byte[] payload, Guid deviceId)
    {
        var json = Encoding.UTF8.GetString(payload);
        _logger.LogDebug("MQTT message on '{Topic}' from device {DeviceId}: {Json}", topic, deviceId, json);

        try
        {
            switch (topic)
            {
                case MqttTopics.Telemetry:
                    await HandleTelemetryAsync(deviceId, json);
                    break;

                case MqttTopics.Attributes:
                    await HandleAttributesAsync(deviceId, json);
                    break;

                default:
                    _logger.LogWarning("Unknown MQTT topic: {Topic}", topic);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic {Topic}", topic);
        }
    }

    private async Task HandleTelemetryAsync(Guid deviceId, string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
        if (values is null || values.Count == 0)
        {
            _logger.LogWarning("Empty telemetry payload from device {DeviceId}", deviceId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var telemetryService = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        await telemetryService.SaveAsync(deviceId, values);
    }

    private async Task HandleAttributesAsync(Guid deviceId, string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (values is null || values.Count == 0)
        {
            _logger.LogWarning("Empty attributes payload from device {DeviceId}", deviceId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var attributeService = scope.ServiceProvider.GetRequiredService<IAttributeService>();
        await attributeService.SaveAsync(deviceId, values, AttributeScope.Client);
    }
}
