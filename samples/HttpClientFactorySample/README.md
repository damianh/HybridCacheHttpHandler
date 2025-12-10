# HttpClient Factory Sample

This sample demonstrates how to integrate `HybridCacheHttpHandler` with ASP.NET Core's `IHttpClientFactory`.

## Overview

The sample shows:
- Registering `HybridCache` in the dependency injection container
- Configuring a named `HttpClient` with the caching handler
- Making HTTP requests that benefit from client-side caching
- Observing cache hits through timing differences

## Running the Sample

```bash
dotnet run
```

The application will make three identical requests to the GitHub API. You'll notice:
1. First request: Full round-trip to the server (slower)
2. Second request: Served from cache (much faster)
3. Third request: Still served from cache (fast)

## Configuration

The caching handler is configured with:
- **DefaultCacheDuration**: 5 minutes for responses without caching headers
- **MaxCacheableContentSize**: 10 MB maximum response size

## Key Code

```csharp
builder.Services
    .AddHttpClient("CachedClient")
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredService<HybridCache>(),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(5),
            MaxCacheableContentSize = 10 * 1024 * 1024
        }
    ));
```

## Learn More

- [IHttpClientFactory documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [HybridCache documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid)
