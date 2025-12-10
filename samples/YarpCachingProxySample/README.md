# YARP Caching Proxy Sample

This sample demonstrates how to build a caching reverse proxy using YARP (Yet Another Reverse Proxy) and `HybridCacheHttpHandler`.

## Overview

The sample shows:
- Building a reverse proxy that caches upstream responses
- Using YARP's `IHttpForwarder` with a custom HTTP client
- Reducing load on upstream services through intelligent caching
- Respecting RFC 7234 cache-control directives

## Running the Sample

```bash
dotnet run
```

The proxy will start on `http://localhost:5000`. Access GitHub API through the proxy:

```bash
# First request - fetches from GitHub
curl http://localhost:5000/api/repos/dotnet/runtime

# Second request - served from cache (faster)
curl http://localhost:5000/api/repos/dotnet/runtime
```

## Configuration

The caching handler is configured with:
- **DefaultCacheDuration**: 10 minutes for responses without caching headers
- **MaxCacheableContentSize**: 50 MB maximum response size

## Architecture

```
Client → YARP Proxy (with HybridCacheHttpHandler) → GitHub API
                ↓
         HybridCache (L1: Memory, L2: Optional Distributed)
```

## Key Code

```csharp
builder.Services.AddHttpClient("YarpCachingClient")
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredService<HybridCache>(),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(10),
            MaxCacheableContentSize = 50 * 1024 * 1024
        }
    ));
```

## Use Cases

This pattern is useful for:
- API gateways that want to reduce upstream load
- Development proxies that cache external API responses
- Edge caching layers for microservices
- Reducing costs from metered APIs

## Learn More

- [YARP documentation](https://microsoft.github.io/reverse-proxy/)
- [HybridCache documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid)
