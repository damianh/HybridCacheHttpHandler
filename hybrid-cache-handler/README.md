# DamianH.HttpHybridCacheHandler

[![NuGet](https://img.shields.io/nuget/v/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/)
[![Downloads](https://img.shields.io/nuget/dt/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/)

RFC 9111 compliant client-side HTTP caching for `HttpClient`, powered by .NET's `HybridCache` for efficient L1 (memory) and L2 (distributed) caching.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Handler Pipeline Configuration](#handler-pipeline-configuration)
  - [Recommended Setup](#recommended-setup)
  - [AutomaticDecompression Explained](#automaticdecompression-explained)
  - [Handler Ordering](#handler-ordering)
  - [Common Mistakes](#common-mistakes)
- [Configuration Options](#configuration-options)
- [Cache Behavior](#cache-behavior)
- [Performance & Memory](#performance--memory)
- [Metrics](#metrics)
- [Benchmarks](#benchmarks)
- [RFC 9111 Conformance Suite](#rfc-9111-conformance-suite)
- [Samples](#samples)

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
dotnet add package DamianH.HttpHybridCacheHandler
```

## Quick Start

### Basic Usage with Recommended Configuration

```csharp
var services = new ServiceCollection();

services.AddHttpHybridCacheHandler(options =>
{
    options.FallbackCacheDuration = TimeSpan.FromMinutes(5);
    options.MaxCacheableContentSize = 10 * 1024 * 1024; // 10MB
    options.CompressionThreshold = 1024; // Compress cached content >1KB
});

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
    .AddHttpMessageHandler(sp => sp.GetRequiredService<HttpHybridCacheHandler>());

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
HttpHybridCacheHandler: receives decompressed content
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
HttpClient → [Outer Handlers] → HttpHybridCacheHandler → SocketsHttpHandler → Network
```

#### With Polly Resilience (Recommended for Production)

```csharp
.AddHttpMessageHandler(sp => new HttpHybridCacheHandler(...))
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
.AddHttpMessageHandler(sp => sp.GetRequiredService<HttpHybridCacheHandler>());
```

To include auth headers in cache key, configure `VaryHeaders` in the `AddHttpHybridCacheHandler` options.

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
.AddHttpMessageHandler(sp => new HttpHybridCacheHandler(...))  // Inner - Wrong!
```

**Correct: Cache handler before Polly**
```csharp
.AddHttpMessageHandler(sp => new HttpHybridCacheHandler(...))  // Inner - Correct!
.AddStandardResilienceHandler()  // Outer
```

**Golden Rule:** `HttpHybridCacheHandler` should receive **decompressed, ready-to-use** content.

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
new HttpHybridCacheHandlerOptions
{
    Mode = CacheMode.Private, // Shares cache across app instances via Redis L2
    FallbackCacheDuration = TimeSpan.FromMinutes(5)
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
new HttpHybridCacheHandlerOptions
{
    Mode = CacheMode.Shared, // RFC 9111 shared cache semantics
    MaxCacheableContentSize = 50 * 1024 * 1024 // 50MB
}
```

### HttpHybridCacheHandlerOptions

- **Mode**: Cache mode determining caching behavior (default: `CacheMode.Private`). Use `CacheMode.Shared` for proxy/CDN scenarios
- **HeuristicFreshnessPercent**: Heuristic freshness percentage for responses with Last-Modified but no explicit freshness info (default: 0.1 or 10%)
- **VaryHeaders**: Headers to include in Vary-aware cache keys (default: Accept, Accept-Encoding, Accept-Language, User-Agent)
- **MaxCacheableContentSize**: Maximum size in bytes for cacheable response content (default: 10 MB). Responses larger than this will not be cached
- **FallbackCacheDuration**: Fallback cache duration for responses without explicit caching headers (default: `TimeSpan.MinValue`, meaning responses without caching headers are not cached)
- **CompressionThreshold**: Minimum content size in bytes to enable compression (default: 1024 bytes). Set to 0 or negative value to disable compression
- **CompressibleContentTypes**: Content types eligible for compression (default: `text/*`, `application/json`, `application/json+*`, `application/xml`, `application/javascript`, `image/svg+xml`)
- **CacheableContentTypes**: Content types eligible for caching (default: `text/*`, `application/json`, `application/json+*`, `application/xml`, `application/javascript`, `application/xhtml+xml`, `image/*`)
- **ContentKeyPrefix**: Prefix for content cache keys (default: `"httpcache:content:"`). Content is stored separately from metadata to avoid Base64 encoding overhead
- **IncludeDiagnosticHeaders**: Whether to include diagnostic headers (`X-Cache-Diagnostic`, etc.) in responses (default: `false`)

## Metrics

The handler emits the following counters via `System.Diagnostics.Metrics` under the meter named `DamianH.HttpHybridCacheHandler`:

| Counter | Description |
|---------|-------------|
| `cache.hits` | Number of cache hits (fresh, revalidated, stale-while-revalidate, stale-if-error) |
| `cache.misses` | Number of cache misses (including cache errors and failed revalidations) |
| `cache.stale` | Number of stale cache entries served (stale-while-revalidate or stale-if-error) |
| `cache.size_exceeded` | Number of responses that exceeded `MaxCacheableContentSize` and were not cached |

All counters include the following tags (following [OpenTelemetry semantic conventions](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/)):

| Tag | Description | Example |
|-----|-------------|---------|
| `http.request.method` | HTTP method | `GET`, `HEAD` |
| `url.scheme` | URL scheme | `http`, `https` |
| `server.address` | Server hostname | `api.example.com` |
| `server.port` | Server port | `443` |

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
- **X-Cache-Age**: Age of cached content in seconds (only for cache hits)
- **X-Cache-MaxAge**: Maximum age of cached content in seconds (only for cache hits)
- **X-Cache-Compressed**: "true" if content was stored compressed (only for cache hits)

Example:
```csharp
var options = new HttpHybridCacheHandlerOptions
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

## RFC 9111 Conformance Suite

The handler is tested against [http-tests/cache-tests](https://github.com/http-tests/cache-tests)
(the HTTP caching test suite behind [cache-tests.fyi](https://cache-tests.fyi), used to assess
browsers, proxies and CDNs). The suite's client sends scripted requests through a minimal YARP
reverse proxy (`conformance/ConformanceProxy`) that uses `HttpHybridCacheHandler` in `Shared` mode.

Run it locally:

```bash
# Windows
./hybrid-cache-handler/conformance/run-conformance.ps1

# Linux/macOS
./hybrid-cache-handler/conformance/run-conformance.sh
```

The script clones the suite (pinned commit), starts the suite's origin server and the proxy, runs
the full suite and compares `results.json` against the checked-in `expected-results.json` baseline.
It fails only on regressions (a test that passed in the baseline now failing). Passing every test
is not a goal — the suite itself documents that full passes are not expected; it measures behavior,
including optional optimizations.

- Debug a single test: `./run-conformance.ps1 -TestId <test-id>`
- After a fix adds new passes: `./run-conformance.ps1 -Update` (or `run-conformance.sh --update`)
  to ratchet the baseline, then commit `expected-results.json`.

The `cache-conformance` CI job runs this on every push/PR and uploads `results.json` as an artifact.

## Samples

See the [`/samples`](../../samples) directory for complete examples:

- [`HttpClientFactorySample`](../../samples/HttpClientFactorySample): Integration with IHttpClientFactory
- [`YarpCachingProxySample`](../../samples/YarpCachingProxySample): Building a caching reverse proxy with YARP
- [`FusionCacheSample`](../../samples/FusionCacheSample): Using FusionCache via its HybridCache adapter for enhanced caching features
- [`FileDistributedCacheSample`](../../samples/FileDistributedCacheSample): File-based L2 cache with HttpHybridCacheHandler
