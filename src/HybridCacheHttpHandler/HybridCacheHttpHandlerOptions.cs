// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.HybridCacheHttpHandler;

/// <summary>
/// Configuration options for <see cref="HybridCacheHttpHandler"/>.
/// </summary>
public class HybridCacheHttpHandlerOptions
{
    /// <summary>
    /// Heuristic freshness percentage for responses with Last-Modified but no explicit freshness info.
    /// Default is 0.1 (10% of Last-Modified age as per RFC 7234 recommendation).
    /// </summary>
    public double HeuristicFreshnessPercent { get; init; } = 0.1;

    /// <summary>
    /// Custom cache key generator function. If null, uses default key generation.
    /// </summary>
    public Func<HttpRequestMessage, string>? CacheKeyGenerator { get; init; }

    /// <summary>
    /// Headers to include in Vary-aware cache keys. If null, uses default set:
    /// Accept, Accept-Encoding, Accept-Language, User-Agent.
    /// </summary>
    public string[]? VaryHeaders { get; init; }

    /// <summary>
    /// Maximum size in bytes for cacheable response content.
    /// Responses larger than this will not be cached.
    /// Default is 10 MB (10,485,760 bytes).
    /// Set to null for unlimited cache size.
    /// </summary>
    public long? MaxCacheableContentSize { get; init; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Default cache duration for responses without explicit caching headers.
    /// If null, responses without caching headers are not cached.
    /// </summary>
    public TimeSpan? DefaultCacheDuration { get; init; }

    /// <summary>
    /// Minimum content size in bytes to enable compression.
    /// Content smaller than this will not be compressed.
    /// Default is 1024 bytes (1 KB).
    /// Set to null to disable compression.
    /// </summary>
    public long? CompressionThreshold { get; init; } = 1024;

    /// <summary>
    /// Content types that are eligible for compression.
    /// If null, uses default compressible types (text/*, application/json, application/xml, etc.).
    /// </summary>
    public string[]? CompressibleContentTypes { get; init; }

    /// <summary>
    /// Content types that are eligible for caching.
    /// If null, all content types are cacheable (subject to other caching rules).
    /// Common cacheable types: text/*, application/json, application/xml, image/*, etc.
    /// </summary>
    public string[]? CacheableContentTypes { get; init; }

    /// <summary>
    /// Whether to include diagnostic headers in responses.
    /// When enabled, adds X-Cache-Diagnostic header with cache behavior information.
    /// Default is false.
    /// </summary>
    public bool IncludeDiagnosticHeaders { get; init; }
}
