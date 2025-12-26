using Target.Endpoints;
using Target.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Services
builder.Services.AddSingleton<ResponseGenerator>();

// Output caching
builder.Services.AddOutputCache();

var app = builder.Build();

// Map health checks
app.MapDefaultEndpoints();

app.UseOutputCache();

// Register endpoint groups
app.MapCacheableEndpoints();
app.MapVaryEndpoints();
app.MapConditionalEndpoints();

app.Run();
