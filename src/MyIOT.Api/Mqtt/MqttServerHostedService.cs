using System.Collections.Concurrent;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MyIOT.Api.Services;

namespace MyIOT.Api.Mqtt;

/// <summary>
/// Hosted service that runs an embedded MQTTnet broker inside the ASP.NET Core app.
/// Devices authenticate via username = accessToken.
/// </summary>
public class MqttServerHostedService : IHostedService, IDisposable
{
    private readonly MqttServer _mqttServer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttMessageHandler _messageHandler;
    private readonly ILogger<MqttServerHostedService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Maps MQTT client ID → device ID (set during authentication).
    /// </summary>
    private readonly ConcurrentDictionary<string, Guid> _clientDeviceMap = new();

    public MqttServerHostedService(
        IServiceScopeFactory scopeFactory,
        MqttMessageHandler messageHandler,
        ILogger<MqttServerHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _messageHandler = messageHandler;
        _logger = logger;
        _configuration = configuration;

        var port = _configuration.GetValue<int>("MqttSettings:Port", 1883);

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        _mqttServer = new MqttFactory().CreateMqttServer(options);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Validate connecting clients
        _mqttServer.ValidatingConnectionAsync += ValidateConnectionAsync;

        // Handle published messages
        _mqttServer.InterceptingPublishAsync += InterceptPublishAsync;

        // Track disconnections
        _mqttServer.ClientDisconnectedAsync += ClientDisconnectedAsync;

        await _mqttServer.StartAsync();

        var port = _configuration.GetValue<int>("MqttSettings:Port", 1883);
        _logger.LogInformation("MQTT broker started on port {Port}", port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttServer.StopAsync();
        _clientDeviceMap.Clear();
        _logger.LogInformation("MQTT broker stopped");
    }

    /// <summary>
    /// Authenticate devices by their access token (passed as MQTT username).
    /// </summary>
    private async Task ValidateConnectionAsync(ValidatingConnectionEventArgs args)
    {
        var accessToken = args.UserName;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            _logger.LogWarning("MQTT connection rejected: no username/accessToken provided (ClientId={ClientId})",
                args.ClientId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var device = await deviceService.GetByAccessTokenAsync(accessToken);

        if (device is null)
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            _logger.LogWarning("MQTT connection rejected: invalid access token (ClientId={ClientId})", args.ClientId);
            return;
        }

        // Store device mapping
        _clientDeviceMap[args.ClientId] = device.Id;
        args.ReasonCode = MqttConnectReasonCode.Success;

        _logger.LogInformation("MQTT client connected: {ClientId} → Device {DeviceId} ({Name})",
            args.ClientId, device.Id, device.Name);
    }

    /// <summary>
    /// Intercept incoming publish messages and route to the message handler.
    /// </summary>
    private async Task InterceptPublishAsync(InterceptingPublishEventArgs args)
    {
        if (!_clientDeviceMap.TryGetValue(args.ClientId, out var deviceId))
        {
            _logger.LogWarning("MQTT publish from unknown client {ClientId}, dropping message", args.ClientId);
            args.ProcessPublish = false;
            return;
        }

        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.PayloadSegment.ToArray();

        await _messageHandler.HandleAsync(topic, payload, deviceId);

        // Don't forward to other subscribers (we're the only consumer)
        args.ProcessPublish = false;
    }

    private Task ClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        _clientDeviceMap.TryRemove(args.ClientId, out _);
        _logger.LogInformation("MQTT client disconnected: {ClientId}", args.ClientId);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mqttServer?.Dispose();
    }
}
