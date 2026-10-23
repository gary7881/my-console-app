namespace MyConsoleApp.Models;

public sealed record MqttReceivedMessage(
    string Topic,
    string Payload,
    DateTimeOffset ReceivedAt);
