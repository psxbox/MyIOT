// ──────────────────────────────────────────────────────────────────
// MyIOT Sample MQTT Test Client
// ──────────────────────────────────────────────────────────────────
// Usage:
//   1. Start the MyIOT.Api application (HTTP + MQTT broker)
//   2. Create a device via POST /api/devices and copy the AccessToken
//   3. Set the ACCESS_TOKEN variable below
//   4. Run:  dotnet run --project samples/MqttTestClient
// ──────────────────────────────────────────────────────────────────

using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;

// ─── Configuration ───
const string brokerHost = "localhost";
const int brokerPort = 1883;

// ⚠️ Replace with the access token from POST /api/devices response
const string ACCESS_TOKEN = "PASTE_YOUR_ACCESS_TOKEN_HERE";

// ─── Create MQTT Client ───
var factory = new MqttFactory();
using var client = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(brokerHost, brokerPort)
    .WithClientId($"test-device-{Guid.NewGuid():N}")
    .WithCredentials(ACCESS_TOKEN) // username = accessToken
    .WithCleanSession()
    .Build();

// ─── Connect ───
Console.WriteLine($"Connecting to MQTT broker at {brokerHost}:{brokerPort}...");

var result = await client.ConnectAsync(options);

if (result.ResultCode != MqttClientConnectResultCode.Success)
{
    Console.WriteLine($"❌ Connection failed: {result.ResultCode}");
    return;
}

Console.WriteLine("✅ Connected successfully!\n");

// ─── Send Telemetry ───
Console.WriteLine("📡 Sending telemetry data...");

for (int i = 0; i < 5; i++)
{
    var telemetry = new Dictionary<string, double>
    {
        ["temperature"] = 20.0 + Random.Shared.NextDouble() * 15.0,
        ["humidity"] = 40.0 + Random.Shared.NextDouble() * 40.0,
        ["pressure"] = 1000.0 + Random.Shared.NextDouble() * 30.0
    };

    var telemetryJson = JsonSerializer.Serialize(telemetry);
    var telemetryMessage = new MqttApplicationMessageBuilder()
        .WithTopic("v1/devices/me/telemetry")
        .WithPayload(Encoding.UTF8.GetBytes(telemetryJson))
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();

    await client.PublishAsync(telemetryMessage);
    Console.WriteLine($"  [{i + 1}/5] Sent: {telemetryJson}");

    await Task.Delay(1000);
}

Console.WriteLine();

// ─── Send Attributes ───
Console.WriteLine("📋 Sending device attributes...");

var attributes = new Dictionary<string, object>
{
    ["firmware"] = "2.1.0",
    ["model"] = "IoT-Sensor-Pro",
    ["serial_number"] = "SN-2026-001234",
    ["location"] = "Building A, Floor 3"
};

var attributesJson = JsonSerializer.Serialize(attributes);
var attributesMessage = new MqttApplicationMessageBuilder()
    .WithTopic("v1/devices/me/attributes")
    .WithPayload(Encoding.UTF8.GetBytes(attributesJson))
    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
    .Build();

await client.PublishAsync(attributesMessage);
Console.WriteLine($"  Sent: {attributesJson}");

Console.WriteLine();

// ─── Disconnect ───
await client.DisconnectAsync();
Console.WriteLine("🔌 Disconnected from MQTT broker.");
Console.WriteLine("\n✅ Test complete! Check the API:");
Console.WriteLine("   GET /api/devices/{id}/telemetry/latest");
Console.WriteLine("   GET /api/devices/{id}/attributes");
