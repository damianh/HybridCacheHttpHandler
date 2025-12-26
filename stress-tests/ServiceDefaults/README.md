# ServiceDefaults

Shared Aspire configuration for stress test services.

## Purpose

This project provides common service defaults for all Aspire-managed services in the stress tests:

- **OpenTelemetry**: Traces, metrics, and logging
- **Health Checks**: Liveness and readiness endpoints
- **Service Discovery**: Automatic service resolution
- **HTTP Resilience**: Retry policies and circuit breakers
- **Observability**: Integration with Aspire Dashboard

## Usage

### In Web Applications (Target)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add service defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Map health check endpoints
app.MapDefaultEndpoints();

app.Run();
```

### In Console Applications (Runner)

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Add service defaults
builder.AddServiceDefaults();

var host = builder.Build();
await host.RunAsync();
```

### In AppHost

```xml
<ProjectReference Include="..\ServiceDefaults\ServiceDefaults.csproj" IsAspireSharedProject="true" />
```

## What's Included

### OpenTelemetry Configuration

- **Traces**: ASP.NET Core and HttpClient instrumentation
- **Metrics**: ASP.NET Core, HttpClient, and Runtime metrics
- **Logs**: Structured logging with OpenTelemetry

All telemetry is exported to the Aspire Dashboard automatically.

### Health Checks

- `/health` - Overall health status (development only)
- `/alive` - Liveness probe (development only)

Custom health checks can be added per service.

### Service Discovery

Automatically resolves service endpoints by name:
- `redis` → Redis connection string
- `target` → Target web application URL

### HTTP Client Defaults

All `HttpClient` instances automatically get:
- **Resilience**: Standard retry policies and circuit breakers
- **Service Discovery**: Resolve endpoints by service name
- **Telemetry**: Automatic trace propagation

## Observability

View telemetry in the Aspire Dashboard:

1. Run `dotnet run` in AppHost
2. Open dashboard (typically http://localhost:15000)
3. View:
   - **Traces**: Request flow across services
   - **Metrics**: Performance counters
   - **Logs**: Structured log messages
   - **Resources**: Service health and status

## Configuration

### Environment Variables

- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP endpoint for traces/metrics
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Azure Application Insights (optional)

### Customization

To add custom health checks in a service:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("my-check", () => HealthCheckResult.Healthy());
```

To add custom metrics:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("MyService"));
```

## Dependencies

- `Microsoft.Extensions.Http.Resilience` - HTTP resilience patterns
- `Microsoft.Extensions.ServiceDiscovery` - Service endpoint resolution
- `OpenTelemetry.*` - Telemetry collection and export

## Notes

- Health check endpoints are **only exposed in development** for security
- OpenTelemetry vulnerability warning (NU1902) is known and being tracked
- This is a shared project (IsAspireSharedProject=true), not a standalone service
