using Prometheus;

namespace MyConsoleApp.Monitoring;

public static class GatewayMetrics
{
    public static readonly Gauge GatewayUp = Metrics.CreateGauge(
        "gateway_up",
        "Whether the gateway has completed startup and is running.");

    public static readonly Gauge ModbusConnected = Metrics.CreateGauge(
        "gateway_modbus_connected",
        "Whether the Modbus TCP server is connected.");

    public static readonly Counter ModbusReads = Metrics.CreateCounter(
        "gateway_modbus_reads_total",
        "Total Modbus read attempts.",
        new CounterConfiguration { LabelNames = ["result"] });

    public static readonly Gauge ModbusTemperature = Metrics.CreateGauge(
        "gateway_modbus_temperature_celsius",
        "Most recently read Modbus temperature in degrees Celsius.");

    public static readonly Counter MqttPublishedMessages = Metrics.CreateCounter(
        "gateway_mqtt_published_messages_total",
        "Total MQTT messages successfully published.",
        new CounterConfiguration { LabelNames = ["type"] });

    public static readonly Counter MqttPublishFailures = Metrics.CreateCounter(
        "gateway_mqtt_publish_failures_total",
        "Total failed MQTT publish attempts.",
        new CounterConfiguration { LabelNames = ["type"] });

    public static readonly Counter MqttReceivedMessages = Metrics.CreateCounter(
        "gateway_mqtt_received_messages_total",
        "Total MQTT messages received from the broker.");

    public static readonly Gauge MqttConnected = Metrics.CreateGauge(
        "gateway_mqtt_connected",
        "Whether the MQTT broker connection is established.");

    public static readonly Gauge PostgreSqlConnected = Metrics.CreateGauge(
        "gateway_postgresql_connected",
        "Whether PostgreSQL storage is available.");

    public static readonly Counter PostgreSqlMessageWrites = Metrics.CreateCounter(
        "gateway_postgresql_message_writes_total",
        "Total MQTT message persistence attempts.",
        new CounterConfiguration { LabelNames = ["result"] });

    public static readonly Gauge LastHeartbeatTimestamp = Metrics.CreateGauge(
        "gateway_heartbeat_last_sent_timestamp_seconds",
        "Unix timestamp when the last heartbeat was successfully published.");
}
