# HttpClient Handler Pipeline Configuration Guide

## Overview

`HybridCacheHttpHandler` is a `DelegatingHandler` that sits in the HttpClient pipeline. Understanding how to configure it with other handlers is crucial for proper functionality.

## Recommended Configuration

### Basic Setup (Recommended)

**Use SocketsHttpHandler with AutomaticDecompression enabled:**

```csharp
builder.Services
    .AddHttpClient("MyClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Enable automatic decompression
        AutomaticDecompression = DecompressionMethods.All,
        
        // Connection pooling settings
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        
        // Reasonable timeouts
        ConnectTimeout = TimeSpan.FromSeconds(10)
    })
    .AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
        sp.GetRequiredService<HybridCache>(),
        TimeProvider.System,
        new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = TimeSpan.FromMinutes(5),
            CompressionThreshold = 1024,
            MaxCacheableContentSize = 10 * 1024 * 1024
        },
        sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
    ));
```

## Why AutomaticDecompression Matters

**Two different compressions:**

1. **Transport Compression** (Server → Client)
   - Controlled by: `AutomaticDecompression` on `SocketsHttpHandler`
   - Purpose: Reduce network bandwidth
   - Handler receives: Decompressed content

2. **Cache Storage Compression** (Our Library)
   - Controlled by: `CompressionThreshold` in options
   - Purpose: Reduce cache storage size
   - Handler stores: Compressed content in cache

**Flow:**
```
Server (gzipped) → SocketsHttpHandler (decompress) → HybridCacheHttpHandler (receives decompressed) 
→ Compress for cache → Store
```

## Handler Ordering

### With Polly Resilience

```csharp
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(...))
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
});
```

**Order:** Polly (outer) → Cache → SocketsHttpHandler

If cache hit, Polly never invoked (fast path).

### With Authentication

```csharp
.AddHttpMessageHandler(() => new AuthenticationHandler())
.AddHttpMessageHandler(sp => new HybridCacheHttpHandler(
    sp.GetRequiredService<HybridCache>(),
    TimeProvider.System,
    new HybridCacheHttpHandlerOptions
    {
        VaryHeaders = new[] { "Authorization", "Accept" }
    },
    sp.GetRequiredService<ILogger<HybridCacheHttpHandler>>()
));
```

Auth headers included in cache key via Vary.

## Common Mistakes

❌ **Wrong:** `AutomaticDecompression = DecompressionMethods.None`
- Cache handler receives compressed content

✅ **Correct:** `AutomaticDecompression = DecompressionMethods.All`
- Cache handler receives decompressed content

❌ **Wrong:** Using `HttpClientHandler` without configuration
✅ **Correct:** Using `SocketsHttpHandler` with explicit settings

## Golden Rule

`HybridCacheHttpHandler` should receive **decompressed, ready-to-use** content from the inner handler.
