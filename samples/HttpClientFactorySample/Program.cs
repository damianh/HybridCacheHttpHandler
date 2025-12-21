// Copyright Damian Hickey

using System.Diagnostics;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add HybridCache
builder.Services.AddHybridCache();

// Configure HttpClient with caching handler
builder.Services
    .AddHttpClient("CachedClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Enable automatic decompression - server compression handled transparently
        AutomaticDecompression = DecompressionMethods.All,
        
        // Connection pooling
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
    })
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredService<HybridCache>(),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(5),
            MaxCacheableContentSize = 10 * 1024 * 1024, // 10MB
            CompressionThreshold = 1024 // Compress cached content >1KB
        },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));

var host = builder.Build();

// Get the HttpClient from the factory
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
var client = httpClientFactory.CreateClient("CachedClient");

Console.WriteLine("HttpClient with Caching Handler Demo");
Console.WriteLine("=====================================\n");

// Make multiple requests to demonstrate caching
var url = "https://api.github.com/repos/dotnet/runtime";

Console.WriteLine($"Request 1: GET {url}");
var sw = Stopwatch.StartNew();
var response1 = await client.GetAsync(url);
sw.Stop();
Console.WriteLine($"Status: {response1.StatusCode}, Time: {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Cache-Control: {response1.Headers.CacheControl}\n");

Console.WriteLine($"Request 2: GET {url} (should be cached)");
sw.Restart();
var response2 = await client.GetAsync(url);
sw.Stop();
Console.WriteLine($"Status: {response2.StatusCode}, Time: {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Age header: {response2.Headers.Age?.TotalSeconds ?? 0}s\n");

Console.WriteLine($"Request 3: GET {url} (should still be cached)");
sw.Restart();
var response3 = await client.GetAsync(url);
sw.Stop();
Console.WriteLine($"Status: {response3.StatusCode}, Time: {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Age header: {response3.Headers.Age?.TotalSeconds ?? 0}s\n");

Console.WriteLine("Notice how requests 2 and 3 are much faster due to caching!");
Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
