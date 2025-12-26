using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runner;
using Runner.Config;
using Runner.Infrastructure;
using Runner.Suites;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

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

// Memory profiling configuration
var memoryConfig = new MemoryProfilingConfig();
builder.Services.AddSingleton(memoryConfig);

// Register suites
builder.Services.AddSingleton<ISuite>(sp => 
    new CacheStampedeSuite(sp.GetRequiredService<MemoryProfilingConfig>()));
builder.Services.AddSingleton<ISuite>(sp => 
    new MemoryLeakDetectionSuite(sp.GetRequiredService<MemoryProfilingConfig>()));

// Infrastructure
builder.Services.AddSingleton<CachedClientFactory>();

// Non-interactive runner for Aspire
builder.Services.AddSingleton<SuiteRunner>();

var host = builder.Build();

// Check if running interactively or via Aspire
var isInteractive = args.Contains("--interactive") || Environment.UserInteractive;

if (isInteractive)
{
    // Interactive mode - wait for user input (for manual testing)
    Console.WriteLine("Starting stress tests in 5 seconds...");
    Console.WriteLine("Press Ctrl+C to cancel");
    await Task.Delay(5000);
}

// Run all suites
var runner = host.Services.GetRequiredService<SuiteRunner>();
var exitCode = await runner.RunAllSuitesAsync();

Environment.Exit(exitCode);
