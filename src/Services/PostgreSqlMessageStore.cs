using MyConsoleApp.Models;
using MyConsoleApp.Monitoring;
using Npgsql;

namespace MyConsoleApp.Services;

public sealed class PostgreSqlMessageStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlMessageStore(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync()
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS mqtt_messages (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                topic TEXT NOT NULL,
                payload TEXT NOT NULL,
                received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var command = _dataSource.CreateCommand(createTableSql);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        GatewayMetrics.PostgreSqlConnected.Set(1);
    }

    public async Task SaveAsync(MqttReceivedMessage message)
    {
        const string insertSql = """
            INSERT INTO mqtt_messages (topic, payload, received_at)
            VALUES ($1, $2, $3);
            """;

        await using var command = _dataSource.CreateCommand(insertSql);
        command.Parameters.AddWithValue(message.Topic);
        command.Parameters.AddWithValue(message.Payload);
        command.Parameters.AddWithValue(message.ReceivedAt);

        try
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            GatewayMetrics.PostgreSqlMessageWrites.WithLabels("success").Inc();
            GatewayMetrics.PostgreSqlConnected.Set(1);
        }
        catch
        {
            GatewayMetrics.PostgreSqlMessageWrites.WithLabels("failure").Inc();
            GatewayMetrics.PostgreSqlConnected.Set(0);
            throw;
        }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
