#nullable enable

using System.Net.Sockets;
using MyConsoleApp.Configuration;
using MyConsoleApp.Helpers;
using MyConsoleApp.Monitoring;
using NModbus;

namespace MyConsoleApp.Services;

public sealed class ModbusTcpService : IDisposable
{
    private readonly ModbusFactory _factory = new();
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;

    public async Task<float> ReadTemperatureAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool readAttempted = false;
            try
            {
                if (_tcpClient?.Connected != true || _master is null)
                {
                    await ConnectAsync(stoppingToken).ConfigureAwait(false);
                }

                IModbusMaster master = _master
                    ?? throw new InvalidOperationException("Modbus master is not connected.");
                readAttempted = true;
                ushort[] registers = master.ReadHoldingRegisters(
                    GatewaySettings.ModbusSlaveId,
                    GatewaySettings.ModbusStartAddress,
                    GatewaySettings.ModbusRegisterCount);
                float temperature = ModbusWordConverter.ToSingle(registers);

                GatewayMetrics.ModbusReads.WithLabels("success").Inc();
                GatewayMetrics.ModbusTemperature.Set(temperature);
                return temperature;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (readAttempted)
                {
                    GatewayMetrics.ModbusReads.WithLabels("failure").Inc();
                }

                ResetConnection();
                Console.WriteLine(
                    $"Modbus connection or read failed: {ex.Message} Retrying in {GatewaySettings.ReconnectInterval.TotalSeconds} seconds.");
                await Task.Delay(GatewaySettings.ReconnectInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        stoppingToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The Modbus read loop stopped unexpectedly.");
    }

    private async Task ConnectAsync(CancellationToken stoppingToken)
    {
        ResetConnection();
        var tcpClient = new TcpClient();
        try
        {
            await tcpClient
                .ConnectAsync(
                    GatewaySettings.ModbusIpAddress,
                    GatewaySettings.ModbusPort,
                    stoppingToken)
                .ConfigureAwait(false);

            _master = _factory.CreateMaster(tcpClient);
            _tcpClient = tcpClient;
            GatewayMetrics.ModbusConnected.Set(1);
            Console.WriteLine(
                $"Connected to Modbus TCP server at {GatewaySettings.ModbusIpAddress}:{GatewaySettings.ModbusPort}.");
        }
        catch
        {
            tcpClient.Dispose();
            GatewayMetrics.ModbusConnected.Set(0);
            throw;
        }
    }

    private void ResetConnection()
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        _master = null;
        _tcpClient = null;
        GatewayMetrics.ModbusConnected.Set(0);
    }

    public void Dispose()
    {
        ResetConnection();
    }
}
