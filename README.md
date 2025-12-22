# HybridCache HTTP Handler

[![NuGet](https://img.shields.io/nuget/v/DamianH.HybridCacheHttpHandler.svg)](https://www.nuget.org/packages/DamianH.HybridCacheHttpHandler/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DamianH.HybridCacheHttpHandler.svg)](https://www.nuget.org/packages/DamianH.HybridCacheHttpHandler/)
[![CI](https://github.com/damianh/HybridCacheHttpHandler/actions/workflows/ci.yml/badge.svg)](https://github.com/damianh/HybridCacheHttpHandler/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-LGPL%20v3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/damianh/HybridCacheHttpHandler.svg)](https://github.com/damianh/HybridCacheHttpHandler/stargazers)

A caching DelegatingHandler for HttpClient that provides client-side HTTP caching based on RFC 9111.

> [!NOTE]  
> Project is under development and is not yet production-ready.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Handler Pipeline Configuration](#handler-pipeline-configuration)
  - [Recommended Setup](#recommended-setup)
  - [AutomaticDecompression Explained](#automaticdecompression-explained)
  - [Handler Ordering](#handler-ordering)
  - [Common Mistakes](#common-mistakes)
- [Multiple HybridCache Instances](#multiple-hybridcache-instances)
- [Configuration Options](#configuration-options)
- [Cache Behavior](#cache-behavior)
- [Performance & Memory](#performance--memory)
- [Metrics](#metrics)
- [Benchmarks](#benchmarks)
- [Samples](#samples)
- [Requirements](#requirements)
- [License](#license)
- [Contributing](#contributing)

## Features

### Core Caching Capabilities
- **RFC 9111 Compliant**: Full implementation of HTTP caching specification for client-side caching
- **HybridCache Integration**: Leverages .NET's HybridCache for efficient L1 (memory) and L2 (distributed) caching
- **Transparent Operation**: Works seamlessly with existing HttpClient code

### Cache-Control Directives

**Request Directives:**
- `max-age`: Control maximum acceptable response age
- `max-stale`: Accept stale responses within specified staleness tolerance
- `min-fresh`: Require responses to remain fresh for specified duration
- `no-cache`: Force revalidation with origin server
- `no-store`: Bypass cache completely
- `only-if-cached`: Return cached responses or 504 if not cached

**Response Directives:**
- `max-age`: Define response freshness lifetime
- `no-cache`: Store but require validation before use
- `no-store`: Prevent caching
- `public`/`private`: Control cache visibility
- `must-revalidate`: Enforce validation when stale

### Advanced Features

- **Conditional Requests**: Automatic ETag (`If-None-Match`) and Last-Modified (`If-Modified-Since`) validation
- **Vary Header Support**: Content negotiation with multiple cache entries per resource
- **Freshness Calculation**: Supports `Expires` header, `Age` header, and heuristic freshness (Last-Modified based)
- **Stale Response Handling**: 
  - `stale-while-revalidate`: Serve stale content while updating in background
  - `stale-if-error`: Serve stale content when origin is unavailable
- **Configurable Limits**: Per-item content size limits (default 10MB)
- **Metrics**: Built-in metrics via `System.Diagnostics.Metrics` for hit/miss rates and cache operations
- **Custom Cache Keys**: Extensible cache key generation for advanced scenarios
- **Request Collapsing**: Prevents cache stampede via `HybridCache.GetOrCreateAsync` automatic request coalescing

## Installation

```bash
dotnet add package HybridCacheHttpHandler
```

## Quick Start

### Basic Usage with Recommended Configuration

```csharp
var services = new ServiceCollection();
services.AddHybridCache();

services.AddHttpClient("MyClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Enable automatic decompression - server compression handled transparently
        AutomaticDecompression = DecompressionMethods.All,
        
        // DNS refresh every 5 minutes - critical for cloud/microservices
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        
        // Close idle connections after 2 minutes
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        
        // Reasonable connection timeout
        ConnectTimeout = TimeSpan.FromSeconds(10)
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

var client = services.BuildServiceProvider()
    .GetRequiredService<IHttpClientFactory>()
    .CreateClient("MyClient");

var response = await client.GetAsync("https://api.example.com/data");
```

## Handler Pipeline Configuration

### Recommended Setup

**Always use `SocketsHttpHandler` with `AutomaticDecompression` enabled** (better performance, DNS refresh, and connection pooling than legacy `HttpClientHandler`):

```csharp
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
})
```

### AutomaticDecompression Explained

**Two different compressions:**

1. **Transport Compression** (Server → Client)
   - Controlled by: `AutomaticDecompression` on `SocketsHttpHandler`
   - Purpose: Reduce network bandwidth
   - Result: Handler receives **decompressed** content

2. **Cache Storage Compression** (This library)
   - Controlled by: `CompressionThreshold` in options
   - Purpose: Reduce cache storage size
   - Result: Content compressed before storing in cache

**Example Flow:**
```
Server sends: gzipped 512 bytes
    ↓
SocketsHttpHandler: auto-decompresses → 2048 bytes
    ↓
HybridCacheHttpHandler: receives decompressed content
    ↓
Our compression: compresses → 600 bytes
    ↓
Cache: stores 600 bytes (no Base64 overhead!)
```

**Benefits:**
- Cache handler can inspect and validate response content
- Cache-Control, ETag, and Last-Modified headers are readable
- Enables intelligent caching decisions
- Storage compression is optional and configurable

### Handler Ordering

**Pipeline structure:**
```
HttpClient → [Outer Handlers] → HybridCacheHttpHandler → SocketsHttpHandler → Network
```

#### With Polly Resilience (Recommended for Production)

```csharp
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
});
```

**Order:** Polly (outer) → Cache → SocketsHttpHandler

**Why:** Cache hit = fast path, Polly never invoked. Cache miss + network failure = Polly retries.

#### With Authentication

```csharp
.AddHttpMessageHandler(() => new AuthenticationHandler())
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
    sp.GetRequiredService<HybridCache>(),
    TimeProvider.System,
    new HybridCacheHttpHandlerOptions
    {
        // Include auth headers in cache key
        VaryHeaders = new[] { "Authorization", "Accept", "Accept-Encoding" }
    },
    sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
));
```

Auth applied before caching, headers included in cache key via Vary.

### Common Mistakes

**Wrong: Not enabling AutomaticDecompression**
```csharp
new SocketsHttpHandler()  // Defaults to None!
```
**Problem:** Cache handler receives compressed content, can't inspect properly.

**Correct: Explicitly enable decompression**
```csharp
new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All
}
```

**Wrong: Using legacy HttpClientHandler**
```csharp
new HttpClientHandler()  // Legacy, less efficient
```

**Correct: Use modern SocketsHttpHandler**
```csharp
new SocketsHttpHandler { /* ... */ }
```

**Wrong: Cache handler after Polly**
```csharp
.AddStandardResilienceHandler()  // Outer
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))  // Inner - Wrong!
```

**Correct: Cache handler before Polly**
```csharp
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))  // Inner - Correct!
.AddStandardResilienceHandler()  // Outer
```

**Golden Rule:** `HybridCacheHttpHandler` should receive **decompressed, ready-to-use** content.

## Multiple HybridCache Instances

Applications may need different `HybridCache` instances for different purposes (HTTP caching, database caching, session data, etc.).

**Solution:** Use Keyed Services to register multiple `HybridCache` instances with different configurations.

### Basic Example

```csharp
// Register multiple caches with different configurations
builder.Services.AddKeyedSingleton("http-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 10 * 1024 * 1024, // 10MB for HTTP responses
        DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5)
        }
    };
    return new HybridCache(options, sp);
});

builder.Services.AddKeyedSingleton("db-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 1024 * 1024, // 1MB for DB queries
        DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(1)
        }
    };
    return new HybridCache(options, sp);
});

// Use keyed cache in HttpClient
builder.Services
    .AddHttpClient("ApiClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredKeyedService<HybridCache>("http-cache"), // Keyed resolution
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(5)
        },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));
```

### Real-World Scenarios

#### Different Retention Policies

```csharp
// Fast-changing data - short cache
builder.Services.AddKeyedSingleton<HybridCache>("stocks-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 5 * 1024 * 1024,
        DefaultEntryOptions = new() { Expiration = TimeSpan.FromSeconds(30) }
    };
    return new HybridCache(options, sp);
});

// Slow-changing data - long cache
builder.Services.AddKeyedSingleton<HybridCache>("products-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 10 * 1024 * 1024,
        DefaultEntryOptions = new() { Expiration = TimeSpan.FromHours(1) }
    };
    return new HybridCache(options, sp);
});

// Different clients with different caches
builder.Services
    .AddHttpClient("StocksClient")
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredKeyedService<HybridCache>("stocks-cache"),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions { DefaultCacheDuration = TimeSpan.FromSeconds(30) },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));

builder.Services
    .AddHttpClient("ProductsClient")
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredKeyedService<HybridCache>("products-cache"),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions { DefaultCacheDuration = TimeSpan.FromHours(1) },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));
```

#### L1-Only vs L1+L2

```csharp
// HTTP caching - L1 only (memory), fast, single instance
builder.Services.AddKeyedSingleton<HybridCache>("http-l1", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        DisableDistributedCache = true, // L1 only
        MaximumPayloadBytes = 10 * 1024 * 1024
    };
    return new HybridCache(options, sp);
});

// Distributed caching - L1+L2, multi-instance
builder.Services.AddKeyedSingleton<HybridCache>("http-l2", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        DisableDistributedCache = false, // L1 + L2
        MaximumPayloadBytes = 10 * 1024 * 1024
    };
    return new HybridCache(options, sp);
});
```

#### Per-Tenant Caching

```csharp
// Each tenant gets its own cache
builder.Services.AddKeyedSingleton<HybridCache>("tenant-a-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 10 * 1024 * 1024,
        DefaultEntryOptions = new() { Expiration = TimeSpan.FromMinutes(10) }
    };
    return new HybridCache(options, sp);
});

builder.Services.AddKeyedSingleton<HybridCache>("tenant-b-cache", (sp, key) =>
{
    var options = new HybridCacheOptions
    {
        MaximumPayloadBytes = 5 * 1024 * 1024,
        DefaultEntryOptions = new() { Expiration = TimeSpan.FromMinutes(5) }
    };
    return new HybridCache(options, sp);
});

// Resolve based on tenant context
builder.Services
    .AddHttpClient("TenantAwareClient")
    .AddHttpMessageHandler(sp =>
    {
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var cacheKey = $"tenant-{tenantContext.TenantId}-cache";
        var cache = sp.GetRequiredKeyedService<HybridCache>(cacheKey);

        return new HybridCacheHttpHandler(
            cache,
            TimeProvider.System,
            new HybridCacheHttpHandlerOptions(),
            sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
        );
    });
```

### Helper Extension Method

Simplify usage with an extension method:

```csharp
public static class HybridCacheExtensions
{
    public static IHttpClientBuilder AddHttpCaching(
        this IHttpClientBuilder builder,
        string cacheKey = "http-cache",
        Action<HybridCacheHttpHandlerOptions>? configure = null)
    {
        return builder.AddHttpMessageHandler(sp =>
        {
            var cache = sp.GetRequiredKeyedService<HybridCache>(cacheKey);
            var options = new HybridCacheHttpHandlerOptions();
            configure?.Invoke(options);

            return new HybridCacheHttpHandler(
                cache,
                TimeProvider.System,
                options,
                sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
            );
        });
    }
}

// Usage
builder.Services
    .AddHttpClient("ApiClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    .AddHttpCaching("api-cache", opts =>
    {
        opts.DefaultCacheDuration = TimeSpan.FromMinutes(5);
        opts.MaxCacheableContentSize = 10 * 1024 * 1024;
    });
```

## Configuration Options

### Cache Mode

The library supports two cache modes, following RFC 9111 semantics:

#### CacheMode.Private (Default)
Browser-like cache behavior suitable for client applications:

**Use Cases:**
- HttpClient in web applications, APIs, background services
- Scaled-out clients sharing cache (multiple instances, serverless/Lambda)
- Per-user/per-tenant caching scenarios

**Behavior:**
- Caches responses with `Cache-Control: private`
- Uses `max-age` directive (ignores `s-maxage`)
- Caches authenticated requests if marked `private` or `max-age`
- Each cache key is client-specific (Vary headers applied)

**Example:**
```csharp
new HybridCacheHttpHandlerOptions
{
    Mode = CacheMode.Private, // Shares cache across app instances via Redis L2
    DefaultCacheDuration = TimeSpan.FromMinutes(5)
}
```

#### CacheMode.Shared
Proxy/CDN-like cache behavior suitable for gateways:

**Use Cases:**
- Reverse proxies (YARP, Envoy)
- API gateways
- Edge caches / CDN-like scenarios

**Behavior:**
- Does NOT cache responses with `Cache-Control: private`
- Prefers `s-maxage` over `max-age`
- Only caches authenticated requests with `public` or `s-maxage`
- Cache is shared across all clients/users

**Example:**
```csharp
new HybridCacheHttpHandlerOptions
{
    Mode = CacheMode.Shared, // RFC 9111 shared cache semantics
    MaxCacheableContentSize = 50 * 1024 * 1024 // 50MB
}
```

### HybridCacheHttpHandlerOptions

- **HeuristicFreshnessPercent**: Heuristic freshness percentage for responses with Last-Modified but no explicit freshness info (default: 0.1 or 10%)
- **CacheKeyGenerator**: Custom cache key generator function (default: uses URL and HTTP method)
- **VaryHeaders**: Headers to include in Vary-aware cache keys (default: Accept, Accept-Encoding, Accept-Language, User-Agent)
- **MaxCacheableContentSize**: Maximum size in bytes for cacheable response content (default: 10 MB). Responses larger than this will not be cached
- **DefaultCacheDuration**: Default cache duration for responses without explicit caching headers (default: null, meaning no caching)
- **CompressionThreshold**: Minimum content size in bytes to enable compression (default: 1024 bytes). Set to null to disable compression
- **CompressibleContentTypes**: Content types eligible for compression (default: text/*, application/json, application/xml, application/javascript, etc.)
- **CacheableContentTypes**: Content types eligible for caching (default: null, all types cacheable). Use this to restrict caching to specific content types like `["application/json", "text/*"]`

## Metrics

The handler emits the following metrics via System.Diagnostics.Metrics:

- `http.client.cache.hit`: Counter for cache hits
- `http.client.cache.miss`: Counter for cache misses
- `http.client.cache.stale`: Counter for stale cache entries served
- `http.client.cache.size_exceeded`: Counter for responses exceeding max size

All metrics include tags:
- `http.request.method`: HTTP method (GET, HEAD, etc.)
- `url.scheme`: URL scheme (http, https)
- `server.address`: Server hostname
- `server.port`: Server port

## Cache Behavior

### Diagnostic Headers

When `IncludeDiagnosticHeaders` is enabled in options, the handler adds diagnostic information to responses:

- **X-Cache-Diagnostic**: Indicates cache behavior for the request
  - `HIT-FRESH`: Served from cache, content is fresh
  - `HIT-REVALIDATED`: Served from cache after successful 304 revalidation
  - `HIT-STALE-WHILE-REVALIDATE`: Served stale while background revalidation occurs
  - `HIT-STALE-IF-ERROR`: Served stale due to backend error
  - `HIT-ONLY-IF-CACHED`: Served from cache with only-if-cached directive
  - `MISS`: Not in cache, fetched from backend
  - `MISS-REVALIDATED`: Cache entry was stale and resource changed
  - `MISS-CACHE-ERROR`: Cache operation failed, bypassed
  - `MISS-ONLY-IF-CACHED`: Not in cache with only-if-cached directive (504 Gateway Timeout)
  - `BYPASS-METHOD`: Request method not cacheable (POST, PUT, etc.)
  - `BYPASS-NO-STORE`: Request has no-store directive
  - `BYPASS-NO-CACHE`: Request has no-cache directive
  - `BYPASS-PRAGMA-NO-CACHE`: Request has Pragma: no-cache header
- **X-Cache-Age**: Age of cached content in seconds (only for cache hits)
- **X-Cache-MaxAge**: Maximum age of cached content in seconds (only for cache hits)
- **X-Cache-Compressed**: "true" if content was stored compressed (only for cache hits)

Example:
```csharp
var options = new HybridCacheHttpHandlerOptions
{
    IncludeDiagnosticHeaders = true
};
```

### Cacheable Responses

Only GET and HEAD requests are cached. Responses are cached when:
- Status code is 200 OK
- Cache-Control allows caching (not no-store, not no-cache without validation)
- Content size is within MaxContentSize limit

### Cache Key Generation

Cache keys are generated from:
- HTTP method
- Request URI
- Vary header values from the response

### Conditional Requests

When serving stale content, the handler automatically adds:
- `If-None-Match` header with cached ETag
- `If-Modified-Since` header with cached Last-Modified date

If the server responds with 304 Not Modified, the cached response is refreshed and served.

## Samples

See the `/samples` directory for complete examples:

- `HttpClientFactorySample`: Integration with IHttpClientFactory
- `YarpCachingProxySample`: Building a caching reverse proxy with YARP
- `FusionCacheSample`: Using FusionCache via its HybridCache adapter for enhanced caching features

## Performance & Memory

The handler is designed for high-performance scenarios with several key optimizations:

### Content/Metadata Separation Architecture

**Eliminates Base64 overhead in distributed cache:**

- **Metadata** (small, ~1-2KB): Status code, headers, timestamps → Stored as JSON
- **Content** (large, variable): Response body → **Stored as raw `byte[]`**
  - **No Base64 encoding** = 33% size savings
  - Content deduplication via SHA256 hash
  - Same content shared across cache entries (different Vary headers)

**Trade-offs:**
- Two cache lookups (metadata + content) vs one lookup
- Acceptable: L1 (memory) cache makes second lookup very fast (~microseconds)
- Benefit: Zero Base64 overhead on all cached content

### Memory Efficiency

- **Stampede Prevention** (via `HybridCache.GetOrCreateAsync`): Multiple concurrent requests for the same resource are automatically collapsed into a single backend request
- **Automatic Deduplication**: Only one request hits the backend while others await the cached result
- Built-in HybridCache feature - no additional configuration needed

### Efficient Caching

- **L1/L2 Strategy**: Fast in-memory (L1) + optional distributed (L2) via HybridCache
- **Size Limits**: Configurable per-item limits (default: 10MB) prevent memory issues
- **Conditional Requests**: ETags and Last-Modified enable efficient 304 responses

### Benchmark Results

See `/benchmarks` for comprehensive memory allocation benchmarks:

| Response Size | Allocations | Gen2 (LOH) | Notes |
|---------------|-------------|------------|-------|
| 1-10KB | ~10-20 KB | 0 | No LOH, optimal |
| 10-85KB | ~20-100 KB | 0 | No LOH, good |
| >85KB | ~100KB+ | >0 | LOH expected, acceptable for reliability |

Run benchmarks: `cd benchmarks && .\run-memory-tests.ps1`

## Benchmarks

Run benchmarks to measure performance:

```bash
dotnet run --project benchmarks/Benchmarks.csproj -c Release
```

## Contributing

Bug reports should be accompanied by a reproducible test case in a pull rquest.
