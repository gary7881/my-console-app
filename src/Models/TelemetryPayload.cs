using System.Text.Json.Serialization;

namespace MyConsoleApp.Models;

public sealed record TelemetryPayload(
    [property: JsonPropertyName("temperature")] float Temperature,
    [property: JsonPropertyName("device_type")] string DeviceType,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
