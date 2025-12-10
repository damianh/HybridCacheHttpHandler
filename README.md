# HybridCache HTTP Handler

A caching DelegatingHandler for HttpClient that provides client-side HTTP caching based on RFC 9111.

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

### Basic Usage

```csharp
var services = new ServiceCollection();
services.AddHybridCache();
services.AddHttpClient("MyClient")
    .AddHttpMessageHandler(sp => new CachingHttpHandler(
        sp.GetRequiredService<HybridCache>()));

var client = services.BuildServiceProvider()
    .GetRequiredService<IHttpClientFactory>()
    .CreateClient("MyClient");

var response = await client.GetAsync("https://api.example.com/data");
```

### With Configuration

```csharp
services.AddHttpClient("MyClient")
    .AddHttpMessageHandler(sp => new CachingHttpHandler(
        sp.GetRequiredService<HybridCache>(),
        sp.GetRequiredService<TimeProvider>(),
        new CachingHttpHandlerOptions
        {
            DefaultExpiration = TimeSpan.FromMinutes(5),
            MaxContentSize = 10 * 1024 * 1024 // 10 MB
        }));
```

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

## Performance Optimizations

The handler is designed for high-performance scenarios with several key optimizations:

### Memory Efficiency
- **ArrayPool Usage**: Byte arrays are rented from `ArrayPool<byte>` and returned after use, reducing GC pressure and avoiding Large Object Heap allocations for responses over 85KB
- **Segmented Buffers**: Large responses are stored in 8KB segments to avoid LOH fragmentation
- **Compression**: Optional GZip compression for cached content reduces memory footprint and cache storage costs
  - Configurable threshold (default: 1KB)
  - Only compresses compressible content types (text, JSON, XML, etc.)
  - Transparent compression/decompression on cache read/write
  - Can be disabled or customized per content type
- **Zero-Copy Streaming**: Cached responses are streamed directly to consumers without intermediate buffering
- **Minimal Allocations**: Uses `Memory<byte>` and `Span<byte>` to avoid unnecessary array allocations during serialization
- **Source-Generated Regex**: Compile-time regex generation for zero-overhead pattern matching
- **Source-Generated Logging**: Compile-time logging code generation for improved performance

### Request Collapsing
- **Stampede Prevention**: Multiple concurrent requests for the same resource are collapsed into a single backend request using `HybridCache.GetOrCreateAsync`
- **Automatic Deduplication**: Only one request hits the backend while others await the cached result

### Efficient Caching
- **L1/L2 Strategy**: HybridCache provides fast in-memory (L1) caching with optional distributed (L2) caching for multi-instance scenarios
- **Size Limits**: Configurable per-item size limits prevent caching oversized responses that could impact memory
- **Conditional Requests**: ETags and Last-Modified headers enable efficient 304 Not Modified responses from origin servers

## Benchmarks

Run benchmarks to measure performance:

```bash
dotnet run --project benchmarks/Benchmarks.csproj -c Release
```
