using System.Text.Json.Serialization;

namespace MyConsoleApp.Models;

public sealed record HeartbeatPayload(
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
