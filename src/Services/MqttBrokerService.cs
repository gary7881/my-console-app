using System.Security;
using HiveMQtt.Client;
using HiveMQtt.Client.Options;
using HiveMQtt.MQTT5.ReasonCodes;
using HiveMQtt.MQTT5.Types;
using MyConsoleApp.Configuration;
using MyConsoleApp.Models;
using MyConsoleApp.Monitoring;

namespace MyConsoleApp.Services;

public sealed class MqttBrokerService
{
    private readonly HiveMQClient _client;
    private readonly PostgreSqlMessageStore _messageStore;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private volatile bool _subscribed;

    public MqttBrokerService(PostgreSqlMessageStore messageStore)
    {
        _messageStore = messageStore;
        var options = new HiveMQClientOptions
        {
            Host = GatewaySettings.MqttHost,
            Port = GatewaySettings.MqttPort,
            UseTLS = true,
            UserName = GatewaySettings.MqttUsername,
            Password = ToSecureString(GatewaySettings.MqttPassword),
            AutomaticReconnect = true,
            ConnectTimeoutInMs = (int)GatewaySettings.MqttConnectTimeout.TotalMilliseconds,
            ResponseTimeoutInMs = (int)GatewaySettings.MqttResponseTimeout.TotalMilliseconds
        };

        _client = new HiveMQClient(options);
        _client.OnMessageReceived += HandleMessageReceivedAsync;
        _client.AfterDisconnect += (_, _) =>
        {
            _subscribed = false;
            GatewayMetrics.MqttConnected.Set(0);
        };
    }

    public async Task<HiveMQtt.Client.Results.PublishResult> PublishAsync(
        string topic,
        string payload,
        CancellationToken stoppingToken)
    {
        string messageType = topic == GatewaySettings.MqttHeartbeatTopic ? "heartbeat" : "telemetry";

        try
        {
            await EnsureConnectedAndSubscribedAsync(stoppingToken).ConfigureAwait(false);
            var result = await _client
                .PublishAsync(topic, payload, QualityOfService.AtLeastOnceDelivery)
                .ConfigureAwait(false);

            GatewayMetrics.MqttConnected.Set(1);
            GatewayMetrics.MqttPublishedMessages.WithLabels(messageType).Inc();
            if (messageType == "heartbeat")
            {
                GatewayMetrics.LastHeartbeatTimestamp.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            return result;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _subscribed = false;
            GatewayMetrics.MqttConnected.Set(0);
            GatewayMetrics.MqttPublishFailures.WithLabels(messageType).Inc();
            throw;
        }
    }

    private async Task EnsureConnectedAndSubscribedAsync(CancellationToken stoppingToken)
    {
        await _connectionLock.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_client.IsConnected())
                    {
                        GatewayMetrics.MqttConnected.Set(0);
                        _subscribed = false;
                        Console.WriteLine($"Connecting to MQTT broker {GatewaySettings.MqttHost}:{GatewaySettings.MqttPort} ...");

                        var connectResult = await _client.ConnectAsync().ConfigureAwait(false);
                        if (connectResult.ReasonCode != ConnAckReasonCode.Success)
                        {
                            throw new InvalidOperationException($"MQTT connection failed: {connectResult}");
                        }
                    }

                    if (!_subscribed)
                    {
                        var subscribeResult = await _client
                            .SubscribeAsync(
                                GatewaySettings.MqttSubscribeTopic,
                                QualityOfService.AtLeastOnceDelivery)
                            .ConfigureAwait(false);
                        _subscribed = true;
                        Console.WriteLine(
                            $"Subscribed to MQTT topic {GatewaySettings.MqttSubscribeTopic}: {subscribeResult}");
                    }

                    GatewayMetrics.MqttConnected.Set(1);
                    return;
                }
                catch (Exception ex)
                {
                    GatewayMetrics.MqttConnected.Set(0);
                    _subscribed = false;
                    Console.WriteLine(
                        $"MQTT connection or subscription failed: {ex.Message} Retrying in {GatewaySettings.ReconnectInterval.TotalSeconds} seconds.");
                }

                await Task.Delay(GatewaySettings.ReconnectInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _connectionLock.Release();
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    private async void HandleMessageReceivedAsync(
        object sender,
        HiveMQtt.Client.Events.OnMessageReceivedEventArgs eventArgs)
    {
        var message = eventArgs.PublishMessage;
        var receivedMessage = new MqttReceivedMessage(
            message.Topic,
            message.PayloadAsString,
            DateTimeOffset.UtcNow);

        GatewayMetrics.MqttReceivedMessages.Inc();
        Console.WriteLine($"Received MQTT message on {receivedMessage.Topic}: {receivedMessage.Payload}");

        try
        {
            await _messageStore.SaveAsync(receivedMessage).ConfigureAwait(false);
            Console.WriteLine("Saved received MQTT message to PostgreSQL.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save MQTT message to PostgreSQL: {ex.Message}");
        }
    }

    private static SecureString ToSecureString(string value)
    {
        var secureValue = new SecureString();
        foreach (char character in value)
        {
            secureValue.AppendChar(character);
        }

        secureValue.MakeReadOnly();
        return secureValue;
    }
}
