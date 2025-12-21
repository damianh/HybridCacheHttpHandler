# HybridCache HTTP Handler

[![NuGet](https://img.shields.io/nuget/v/DamianH.HybridCacheHttpHandler.svg)](https://www.nuget.org/packages/DamianH.HybridCacheHttpHandler/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DamianH.HybridCacheHttpHandler.svg)](https://www.nuget.org/packages/DamianH.HybridCacheHttpHandler/)
[![CI](https://github.com/damianh/HybridCacheHttpHandler/actions/workflows/ci.yml/badge.svg)](https://github.com/damianh/HybridCacheHttpHandler/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-LGPL%20v3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/damianh/HybridCacheHttpHandler.svg)](https://github.com/damianh/HybridCacheHttpHandler/stargazers)

A caching DelegatingHandler for HttpClient that provides client-side HTTP caching based on RFC 9111.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Handler Pipeline Configuration](#handler-pipeline-configuration)
  - [Recommended Setup](#recommended-setup)
  - [Why SocketsHttpHandler?](#why-socketshttphandler)
  - [AutomaticDecompression Explained](#automaticdecompression-explained)
  - [Handler Ordering](#handler-ordering)
  - [Common Mistakes](#common-mistakes)
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
- **Request Collapsing**: Prevents cache stampede with automatic request coalescing

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

**Always use `SocketsHttpHandler` with `AutomaticDecompression` enabled:**

```csharp
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
})
```

### Why SocketsHttpHandler?

`SocketsHttpHandler` is the modern, high-performance HTTP handler. `HttpClientHandler` is legacy.

| Feature | SocketsHttpHandler | HttpClientHandler |
|---------|-------------------|-------------------|
| **Performance** | ✅ Higher throughput, lower latency | ⚠️ Slower |
| **DNS Refresh** | ✅ Built-in (`PooledConnectionLifetime`) | ❌ Manual workarounds |
| **Connection Pooling** | ✅ Advanced, configurable | ⚠️ Basic |
| **Cross-platform** | ✅ Consistent behavior | ❌ Varies by OS |
| **.NET 5+** | ✅ Recommended | ⚠️ Legacy wrapper |

**Critical for SOA/Microservices:**
- **DNS Refresh**: Services scale, IPs change. Without refresh, connections stick to old/dead instances.
- **Performance**: ~40% higher throughput vs HttpClientHandler
- **Reliability**: Better connection pool management prevents port exhaustion

### AutomaticDecompression Explained

**Two different compressions:**

1. **Transport Compression** (Server → Client)
   - Controlled by: `AutomaticDecompression` on `SocketsHttpHandler`
   - Purpose: Reduce network bandwidth
   - Result: Handler receives **decompressed** content

2. **Cache Storage Compression** (Our Library)
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

**Why this matters:**
- ✅ Cache handler can inspect/validate response content
- ✅ Cache-Control, ETag, Last-Modified headers readable
- ✅ Intelligent caching decisions possible
- ✅ Our compression is optional and controlled

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

❌ **Wrong: Not enabling AutomaticDecompression**
```csharp
new SocketsHttpHandler()  // Defaults to None!
```
**Problem:** Cache handler receives compressed content, can't inspect properly.

✅ **Correct: Explicitly enable decompression**
```csharp
new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All
}
```

❌ **Wrong: Using legacy HttpClientHandler**
```csharp
new HttpClientHandler()  // Legacy, less efficient
```

✅ **Correct: Use modern SocketsHttpHandler**
```csharp
new SocketsHttpHandler { /* ... */ }
```

❌ **Wrong: Cache handler after Polly**
```csharp
.AddStandardResilienceHandler()  // Outer
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))  // Inner - Wrong!
```

✅ **Correct: Cache handler before Polly**
```csharp
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))  // Inner - Correct!
.AddStandardResilienceHandler()  // Outer
```

**Golden Rule:** `HybridCacheHttpHandler` should receive **decompressed, ready-to-use** content.

## Configuration Options

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

## Requirements

- .NET 10.0 or later

## License

Licensed under the Apache License 2.0. See LICENSE file for details.

## Contributing

Contributions are welcome. Please ensure tests pass before submitting pull requests.

```bash
dotnet run --project test/Tests/Tests.csproj
```

## Performance & Memory

The handler is designed for high-performance scenarios with several key optimizations:

### Content/Metadata Separation Architecture

**Eliminates Base64 overhead in distributed cache:**

- **Metadata** (small, ~1-2KB): Status code, headers, timestamps → Stored as JSON
- **Content** (large, variable): Response body → **Stored as raw `byte[]`**
  - ✅ **No Base64 encoding** = 33% size savings
  - ✅ Content deduplication via SHA256 hash
  - ✅ Same content shared across cache entries (different Vary headers)

**Trade-offs:**
- Two cache lookups (metadata + content) vs one lookup
- Acceptable: L1 (memory) cache makes second lookup very fast (~microseconds)
- Benefit: Zero Base64 overhead on all cached content

### Memory Efficiency

- **SegmentedBuffer**: Large responses read in 80KB segments (below 85KB LOH threshold)
- **Compression**: Optional GZip compression for cached content
  - Configurable threshold (default: 1KB)
  - Only compresses compressible content types (text, JSON, XML)
  - Reduces memory footprint and cache storage costs
- **ArrayPool Usage**: Byte arrays rented from `ArrayPool<byte>` during read operations
- **Zero-Copy Streaming**: Cached responses streamed directly without intermediate buffering
- **Source-Generated Regex & Logging**: Compile-time generation for zero runtime overhead

**LOH Considerations:**
- Content >85KB will hit Large Object Heap (expected and acceptable)
- Most API responses <85KB avoid LOH
- Compression helps reduce LOH pressure for compressible content
- Trade-off: Reliability (caching large responses) > LOH cost

### Request Collapsing

- **Stampede Prevention**: Multiple concurrent requests for same resource collapsed into single backend request
- **Automatic Deduplication**: Only one request hits backend while others await cached result
- Uses `HybridCache.GetOrCreateAsync` for built-in request coalescing

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
