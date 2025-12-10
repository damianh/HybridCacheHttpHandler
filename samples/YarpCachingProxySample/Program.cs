// Copyright Damian Hickey

using DamianH.HybridCacheHttpHandler;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

// Add HybridCache
builder.Services.AddHybridCache();

// Configure HTTP client for YARP with caching handler
builder.Services.AddHttpClient("YarpCachingClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>(),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(10),
            MaxCacheableContentSize = 50 * 1024 * 1024 // 50MB
        },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));

// Add YARP with direct forwarding
builder.Services.AddHttpForwarder();

var app = builder.Build();

// Get the forwarder and HTTP client factory
var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient("YarpCachingClient");

// Map a route that forwards to GitHub API
app.Map("/api/{**catch-all}", async httpContext =>
{
    var destinationPrefix = "https://api.github.com/";
    await forwarder.SendAsync(httpContext, destinationPrefix, httpClient);
});

Console.WriteLine("YARP Caching Proxy running on http://localhost:5000");
Console.WriteLine("Try: http://localhost:5000/api/repos/dotnet/runtime");
Console.WriteLine("\nProxy will cache responses from GitHub API");

app.Run();
