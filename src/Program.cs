using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MyConsoleApp.Configuration;
using MyConsoleApp.Models;
using MyConsoleApp.Monitoring;
using MyConsoleApp.Services;
using Prometheus;

namespace MyConsoleApp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Modbus MQTT gateway...");

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(GatewaySettings.MetricsListenUrl);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "ModbusMqttGateway";
        });

        await using var app = builder.Build();
        app.UseHttpMetrics();
        app.MapMetrics();

        GatewayMetrics.GatewayUp.Set(0);
        GatewayMetrics.ModbusConnected.Set(0);
        GatewayMetrics.MqttConnected.Set(0);
        GatewayMetrics.PostgreSqlConnected.Set(0);
        await app.StartAsync().ConfigureAwait(false);
        Console.WriteLine($"Prometheus metrics available at {GatewaySettings.MetricsListenUrl}/metrics");

        try
        {
            await RunGatewayAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
        }
        finally
        {
            GatewayMetrics.GatewayUp.Set(0);
            await app.StopAsync().ConfigureAwait(false);
        }
    }

    private static async Task RunGatewayAsync(CancellationToken stoppingToken)
    {
        string connectionString = GatewaySettings.GetPostgreSqlConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing required environment variable: {GatewaySettings.PostgreSqlConnectionStringVariable}.");
        }

        await using var messageStore = new PostgreSqlMessageStore(connectionString);
        try
        {
            await messageStore.InitializeAsync().ConfigureAwait(false);
            Console.WriteLine("Connected to PostgreSQL and verified mqtt_messages table.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to prepare PostgreSQL storage: {ex.Message}");
            throw new InvalidOperationException("Failed to prepare PostgreSQL storage.", ex);
        }

        using var modbusService = new ModbusTcpService();
        var mqttService = new MqttBrokerService(messageStore);

        GatewayMetrics.GatewayUp.Set(1);
        await Task.WhenAll(
            RunTelemetryLoopAsync(modbusService, mqttService, stoppingToken),
            RunHeartbeatLoopAsync(mqttService, stoppingToken)).ConfigureAwait(false);
    }

    private static async Task RunTelemetryLoopAsync(
        ModbusTcpService modbusService,
        MqttBrokerService mqttService,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                float temperature = await modbusService
                    .ReadTemperatureAsync(stoppingToken)
                    .ConfigureAwait(false);
                var telemetry = new TelemetryPayload(
                    temperature,
                    "modbus",
                    DateTimeOffset.UtcNow);
                string payload = JsonSerializer.Serialize(telemetry);

                var result = await mqttService
                    .PublishAsync(GatewaySettings.MqttPublishTopic, payload, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine($"Published to {GatewaySettings.MqttPublishTopic}: {payload} (Result: {result})");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading Modbus data or publishing MQTT message: {ex.Message}");
            }

            await Task.Delay(GatewaySettings.PublishInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static async Task RunHeartbeatLoopAsync(
        MqttBrokerService mqttService,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = new HeartbeatPayload(
                    GatewaySettings.DeviceId,
                    "online",
                    DateTimeOffset.UtcNow);
                string payload = JsonSerializer.Serialize(heartbeat);

                var result = await mqttService
                    .PublishAsync(GatewaySettings.MqttHeartbeatTopic, payload, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"Published heartbeat to {GatewaySettings.MqttHeartbeatTopic}: {payload} (Result: {result})");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while publishing MQTT heartbeat: {ex.Message}");
            }

            await Task.Delay(GatewaySettings.HeartbeatInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
