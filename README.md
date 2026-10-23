# My Console App

This is a simple C# console application designed to demonstrate the basic structure and functionality of a console app.

## Overview

The My Console App serves as an introductory project for understanding how to create and run a C# console application. It includes essential components such as the entry point, project configuration, and basic input/output operations.

## Source Structure

- `src/Program.cs` coordinates application startup and the Modbus-to-MQTT publishing loop.
- `src/Configuration/` contains gateway connection and topic settings.
- `src/Helpers/` contains Modbus register conversion utilities.
- `src/Models/` contains MQTT message and telemetry data models.
- `src/Monitoring/` contains Prometheus metrics for gateway and dependency health.
- `src/Services/` contains the Modbus TCP client, MQTT broker client, and PostgreSQL message store.

The gateway publishes a heartbeat message to `hivemqdemo/heartbeat` every 30 seconds. The JSON payload contains the device ID, online status, and UTC timestamp.
MQTT connection attempts and broker responses are time-limited so a stale long-running connection cannot block heartbeat publishing indefinitely; failed publishes are logged and retried by the heartbeat loop.

## Prometheus and Grafana Monitoring

The application exposes Prometheus metrics at `http://localhost:9184/metrics`. The exporter listens on all interfaces so Prometheus running in Docker Desktop can scrape the host application. Restrict access to port `9184` with your host firewall if the machine is reachable from untrusted networks.

Metrics include gateway and Modbus/MQTT/PostgreSQL connection status, Modbus read results and latest temperature, MQTT publish/receive counts and failures, PostgreSQL message-write results, and the last successful heartbeat time.

Start Prometheus and Grafana from the repository root. Set a non-default Grafana administrator password first:

```powershell
$env:GRAFANA_ADMIN_PASSWORD = "choose-a-strong-password"
docker compose -f monitoring/docker-compose.yml up -d
```

Then run the gateway as usual. Open Prometheus at `http://localhost:9090` and Grafana at `http://localhost:3000` (user `admin`, password from `GRAFANA_ADMIN_PASSWORD`). Grafana automatically provisions the Prometheus data source and the **Modbus MQTT Gateway** dashboard.

## Getting Started

To get started with this project, follow the instructions below:

### Prerequisites

- .NET SDK (version 5.0 or later) installed on your machine.
- A code editor or IDE of your choice (e.g., Visual Studio Code).

### Building the Project

1. Open a terminal and navigate to the project directory:
   ```
   cd my-console-app
   ```

2. Restore the project dependencies:
   ```
   dotnet restore
   ```

3. Build the project:
   ```
   dotnet build
   ```

### Running the Application

Set the PostgreSQL connection string in the `PGSQL_CONNECTION_STRING` environment variable before running. The app creates the `mqtt_messages` table if it does not already exist, then stores each subscribed MQTT message's topic, payload, and receive timestamp.

PowerShell example:
```powershell
$env:PGSQL_CONNECTION_STRING = "Host=localhost;Port=5432;Database=mydb;Username=myuser;Password=mypassword"
```

To run the application, use the following command in the terminal:
```
dotnet run --project MyConsoleApp.csproj
```

## Contributing

If you would like to contribute to this project, please feel free to submit a pull request or open an issue.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.