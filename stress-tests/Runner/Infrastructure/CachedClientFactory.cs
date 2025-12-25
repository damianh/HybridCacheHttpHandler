using System.Net;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Runner.Infrastructure;

public class CachedClientFactory(
    HybridCache hybridCache,
    IConfiguration configuration,
    ILogger<HybridCacheHttpHandler> logger)
{
    public HttpClient CreateClient(HybridCacheHttpHandlerOptions? options = null)
    {
        options ??= new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(5),
            MaxCacheableContentSize = 10 * 1024 * 1024,
            CompressionThreshold = 1024,
            IncludeDiagnosticHeaders = true
        };

        var handler = new HybridCacheHttpHandler(
            hybridCache,
            TimeProvider.System,
            options,
            logger)
        {
            InnerHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
            }
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(configuration["TARGET_URL"] ?? "http://localhost:5001")
        };

        return client;
    }

    public HttpClient CreateClientWithoutCache()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(configuration["TARGET_URL"] ?? "http://localhost:5001")
        };
    }
}
