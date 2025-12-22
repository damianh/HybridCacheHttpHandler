using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runner.Infrastructure;
using Runner.Menu;
using Runner.Suites;

var builder = Host.CreateApplicationBuilder(args);

// Add service discovery for Redis connection
builder.Services.AddServiceDiscovery();

// Add Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Connection string resolved via Aspire service discovery
    options.Configuration = builder.Configuration.GetConnectionString("redis");
    options.InstanceName = "StressTests:";
});

// Add HybridCache with Redis L2
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 10 * 1024 * 1024; // 10MB
    options.MaximumKeyLength = 1024;
    options.DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1) // L1 cache shorter than L2
    };
});

// Register suites
builder.Services.AddSingleton<ISuite, CacheStampedeSuite>();

// Infrastructure
builder.Services.AddSingleton<CachedClientFactory>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<ResultsPresenter>();

// Menu
builder.Services.AddSingleton<InteractiveMenu>();

var host = builder.Build();

// Run interactive menu
var menu = host.Services.GetRequiredService<InteractiveMenu>();
await menu.RunAsync();
