namespace MyConsoleApp.Configuration;

public static class GatewaySettings
{
    public const string ModbusIpAddress = "192.168.0.229";
    public const int ModbusPort = 502;
    public const byte ModbusSlaveId = 21;
    public const ushort ModbusStartAddress = 0;
    public const ushort ModbusRegisterCount = 2;

    public const string MqttHost = "86b28c9e430741d4bffdf08d22f3d26a.s1.eu.hivemq.cloud";
    public const int MqttPort = 8883;
    public const string MqttUsername = "nrf_device";
    public const string MqttPassword = "86012304";
    public const string MqttPublishTopic = "hivemqdemo/telemetry";
    public const string MqttHeartbeatTopic = "hivemqdemo/heartbeat";
    public const string MqttSubscribeTopic = "hivemqdemo/commands/#";
    public const string DeviceId = "modbus-gateway";
    public const string MetricsListenUrl = "http://0.0.0.0:9184";

    public const string PostgreSqlConnectionStringVariable = "PGSQL_CONNECTION_STRING";
    public static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MqttConnectTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MqttResponseTimeout = TimeSpan.FromSeconds(15);

    public static string GetPostgreSqlConnectionString() =>
        Environment.GetEnvironmentVariable(PostgreSqlConnectionStringVariable);
}
