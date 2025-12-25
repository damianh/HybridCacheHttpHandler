// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace DamianH.HybridCacheHttpHandler;

/// <summary>
/// An HTTP delegating handler that provides client-side caching based on RFC 9111.
/// </summary>
public class HybridCacheHttpHandler : DelegatingHandler
{
    private readonly HybridCache _cache;
    private readonly ContentCache _contentCache;
    private readonly TimeProvider _timeProvider;
    private readonly HybridCacheHttpHandlerOptions _options;
    private readonly ILogger _logger;
    private static readonly Meter Meter = new(
        nameof(HybridCacheHttpHandler),
        typeof(HybridCacheHttpHandler).Assembly.GetName().Version?.ToString() ?? "1.0.0");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("cache.hits", description: "Number of cache hits");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("cache.misses", description: "Number of cache misses");

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridCacheHttpHandler"/> class.
    /// </summary>
    /// <param name="cache">The hybrid cache instance to use for caching.</param>
    /// <param name="timeProvider">The time provider for time-based operations. Uses system time if not specified.</param>
    /// <param name="options">Configuration options for the handler. Uses default options if not specified.</param>
    /// <param name="logger">The logger instance. Uses NullLogger if not specified.</param>
    public HybridCacheHttpHandler(
        HybridCache cache,
        TimeProvider timeProvider,
        HybridCacheHttpHandlerOptions options,
        ILogger<HybridCacheHttpHandler> logger)
    {
        _cache = cache;
        _contentCache = new ContentCache(cache, options.ContentKeyPrefix);
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridCacheHttpHandler"/> class with a specific inner handler.
    /// </summary>
    /// <param name="innerHandler">The inner handler which is responsible for processing the HTTP response messages.</param>
    /// <param name="cache">The hybrid cache instance to use for caching.</param>
    /// <param name="timeProvider">The time provider for time-based operations. Uses system time if not specified.</param>
    /// <param name="options">Configuration options for the handler. Uses default options if not specified.</param>
    /// <param name="logger">The logger instance. Uses NullLogger if not specified.</param>
    public HybridCacheHttpHandler(
        HttpMessageHandler innerHandler,
        HybridCache cache,
        TimeProvider timeProvider,
        HybridCacheHttpHandlerOptions options,
        ILogger<HybridCacheHttpHandler> logger)
        : base(innerHandler)
    {
        _cache = cache;
        _contentCache = new ContentCache(cache, options.ContentKeyPrefix);
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        Ct ct)
    {
        // Only cache GET and HEAD requests
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            var response = await base.SendAsync(request, ct);
            AddDiagnosticHeaders(response, "BYPASS-METHOD");
            return response;
        }

        // Check request Cache-Control directives
        var requestCacheControl = request.Headers.CacheControl;

        // Handle Pragma: no-cache (HTTP/1.0 compatibility - RFC 7234 §5.4)
        if (request.Headers.TryGetValues("Pragma", out var pragmaValues))
        {
            if (pragmaValues.Any(v => v.Equals("no-cache", StringComparison.OrdinalIgnoreCase)))
            {
                // Treat as Cache-Control: no-cache
                var response = await base.SendAsync(request, ct);
                AddDiagnosticHeaders(response, "BYPASS-PRAGMA-NO-CACHE");
                return response;
            }
        }

        // Handle only-if-cached
        if (requestCacheControl?.OnlyIfCached == true)
        {
            var cacheKey = GenerateVaryAwareCacheKey(request);

            var cachedEntry = await _cache.GetOrCreateAsync<CachedHttpMetadata?>(
                cacheKey,
                _ => ValueTask.FromResult<CachedHttpMetadata?>(null),
                cancellationToken: ct
            );

            if (cachedEntry != null)
            {
                var response = await DeserializeResponseAsync(cachedEntry, ct);
                if (response != null)
                {
                    AddDiagnosticHeaders(response, "HIT-ONLY-IF-CACHED", cachedEntry);
                    return response;
                }
                // Content was missing, metadata cleaned up in DeserializeResponseAsync
            }

            // Return 504 Gateway Timeout if not in cache
            var gatewayTimeout = new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                RequestMessage = request
            };
            AddDiagnosticHeaders(gatewayTimeout, "MISS-ONLY-IF-CACHED");
            return gatewayTimeout;
        }

        // Handle no-store - bypass cache entirely
        if (requestCacheControl?.NoStore == true)
        {
            var response = await base.SendAsync(request, ct);
            AddDiagnosticHeaders(response, "BYPASS-NO-STORE");
            return response;
        }

        // Handle no-cache or max-age=0 - require validation
        var mustRevalidate = requestCacheControl?.NoCache == true || requestCacheControl?.MaxAge == TimeSpan.Zero;

        var cacheKey2 = GenerateVaryAwareCacheKey(request);
        HttpResponseMessage? uncachedResponse = null;

        CachedHttpMetadata? cachedResponse;
        try
        {
            cachedResponse = await _cache.GetOrCreateAsync(
                cacheKey2,
                async cancel =>
                {
                    uncachedResponse = await base.SendAsync(request, cancel);

                    // Don't cache if request had no-store
                    if (request.Headers.CacheControl?.NoStore == true)
                    {
                        return null;
                    }

                    // Authorization header handling depends on cache mode
                    if (request.Headers.Authorization != null)
                    {
                        var responseCacheControl = uncachedResponse.Headers.CacheControl;

                        if (_options.Mode == CacheMode.Shared)
                        {
                            // Shared cache: Only cache if explicitly marked public or has s-maxage
                            if (responseCacheControl?.Public != true &&
                                responseCacheControl?.SharedMaxAge == null)
                            {
                                return null;
                            }
                        }
                        else // CacheMode.Private
                        {
                            // Private cache: Require explicit public or private directive for Authorization requests
                            if (responseCacheControl?.Public != true &&
                                responseCacheControl?.Private != true)
                            {
                                return null;
                            }
                        }
                    }

                    // Check if response is cacheable
                    if (!IsResponseCacheable(uncachedResponse))
                    {
                        return null;
                    }

                    return await SerializeResponse(uncachedResponse, request);
                },
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            // Cache read/write failure - fall back to origin
            _logger.CacheOperationFailed(request.RequestUri, ex);
            uncachedResponse ??= await base.SendAsync(request, ct);
            AddDiagnosticHeaders(uncachedResponse, "MISS-CACHE-ERROR");
            CacheMisses.Add(1);
            return uncachedResponse;
        }

        // If we got a cached response, check if it's fresh
        if (cachedResponse != null)
        {
            // If factory just ran (uncachedResponse != null), return the fresh response
            if (uncachedResponse != null)
            {
                // Factory ran, so this is a miss that we just cached
                AddDiagnosticHeaders(uncachedResponse, "MISS");
                CacheMisses.Add(1);
                return uncachedResponse;
            }

            // From here, uncachedResponse is null, meaning we have a cache hit
            // Check if validation is required (no-cache request or no-cache response)
            if (mustRevalidate || cachedResponse.NoCache)
            {
                var validationRequest = CreateValidationRequest(request, cachedResponse);
                uncachedResponse = await base.SendAsync(validationRequest, ct);

                // Handle 304 Not Modified
                if (uncachedResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    var updatedEntry = UpdateCachedEntry(cachedResponse, uncachedResponse);
                    try
                    {
                        await _cache.SetAsync(cacheKey2, updatedEntry, cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.CacheWriteFailed(request.RequestUri, ex);
                    }

                    CacheHits.Add(1);
                    var response = await DeserializeResponseAsync(updatedEntry, ct);
                    if (response == null)
                    {
                        // Content missing, return fresh response
                        AddDiagnosticHeaders(uncachedResponse, "MISS-CACHE-ERROR");
                        return uncachedResponse;
                    }
                    AddDiagnosticHeaders(response, "HIT-REVALIDATED", updatedEntry);
                    return response;
                }

                // Got a new response, cache it
                CacheMisses.Add(1);
                AddDiagnosticHeaders(uncachedResponse, "MISS-REVALIDATED");
                return uncachedResponse;
            }

            if (IsFresh(cachedResponse, request))
            {
                // Cache hit on fresh response
                CacheHits.Add(1);
                var response = await DeserializeResponseAsync(cachedResponse, ct);
                if (response == null)
                {
                    // Content missing - treat as cache miss
                    await _cache.RemoveAsync(cacheKey2, ct);
                    var freshResponse = await base.SendAsync(request, ct);
                    AddDiagnosticHeaders(freshResponse, "MISS-CACHE-ERROR");
                    CacheMisses.Add(1);
                    return freshResponse;
                }
                AddDiagnosticHeaders(response, "HIT-FRESH", cachedResponse);
                return response;
            }

            // Response is stale, check stale-while-revalidate
            if (cachedResponse.StaleWhileRevalidate.HasValue)
            {
                var age = _timeProvider.GetUtcNow() - cachedResponse.CachedAt;
                var freshnessLifetime = cachedResponse.MaxAge ?? TimeSpan.Zero;
                var staleness = age - freshnessLifetime;

                // Within stale-while-revalidate window?
                if (staleness <= cachedResponse.StaleWhileRevalidate.Value)
                {
                    // Serve stale content immediately
                    var staleResponse = await DeserializeResponseAsync(cachedResponse, ct);
                    if (staleResponse == null)
                    {
                        // Content missing - treat as cache miss
                        await _cache.RemoveAsync(cacheKey2, ct);
                        var freshResponse = await base.SendAsync(request, ct);
                        AddDiagnosticHeaders(freshResponse, "MISS-CACHE-ERROR");
                        CacheMisses.Add(1);
                        return freshResponse;
                    }
                    AddDiagnosticHeaders(staleResponse, "HIT-STALE-WHILE-REVALIDATE", cachedResponse);

                    // Trigger background revalidation
                    _ = Task.Run(() => BackgroundRevalidateAsync(cachedResponse, request, cacheKey2), ct);

                    CacheHits.Add(1); // Count as hit (stale-while-revalidate)
                    return staleResponse;
                }
            }

            // Response is stale, attempt validation
            var staleValidationRequest = CreateValidationRequest(request, cachedResponse);

            uncachedResponse = await base.SendAsync(staleValidationRequest, ct);

            // Check for stale-if-error
            if ((int)uncachedResponse.StatusCode >= 500 &&
                cachedResponse is { StaleIfError: not null, MustRevalidate: false })
            {
                var age = _timeProvider.GetUtcNow() - cachedResponse.CachedAt;
                var freshnessLifetime = cachedResponse.MaxAge ?? TimeSpan.Zero;
                var staleness = age - freshnessLifetime;

                // Within stale-if-error window?
                if (staleness <= cachedResponse.StaleIfError.Value)
                {
                    CacheHits.Add(1); // Count as hit (stale-if-error)
                    var response = await DeserializeResponseAsync(cachedResponse, ct);
                    if (response == null)
                    {
                        // Content missing - return error response
                        AddDiagnosticHeaders(uncachedResponse, "MISS-CACHE-ERROR");
                        return uncachedResponse;
                    }
                    AddDiagnosticHeaders(response, "HIT-STALE-IF-ERROR", cachedResponse);
                    return response;
                }
            }

            // Handle 304 Not Modified
            if (uncachedResponse.StatusCode == HttpStatusCode.NotModified)
            {
                // Update cached entry with new metadata from 304 response
                var updatedEntry = UpdateCachedEntry(cachedResponse, uncachedResponse);
                try
                {
                    await _cache.SetAsync(cacheKey2, updatedEntry, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    // Cache write failure - ignore, still return the response
                    _logger.CacheWriteFailed(request.RequestUri, ex);
                }

                CacheHits.Add(1); // Count as hit (revalidated)
                // Return cached body with updated metadata
                var response = await DeserializeResponseAsync(updatedEntry, ct);
                if (response == null)
                {
                    // Content missing - return fresh response
                    AddDiagnosticHeaders(uncachedResponse, "MISS-CACHE-ERROR");
                    return uncachedResponse;
                }
                AddDiagnosticHeaders(response, "HIT-REVALIDATED", updatedEntry);
                return response;
            }

            // Resource changed (200 or other status) - update cache if cacheable
            if (IsResponseCacheable(uncachedResponse))
            {
                var freshResponse = await SerializeResponse(uncachedResponse, staleValidationRequest);
                if (freshResponse != null)
                {
                    try
                    {
                        await _cache.SetAsync(cacheKey2, freshResponse, cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        // Cache write failure - ignore, still return the response
                        _logger.CacheWriteFailed(request.RequestUri, ex);
                    }
                }
            }
            else
            {
                // Response has no-store or is not cacheable, remove existing cache entry
                var responseCacheControl = uncachedResponse.Headers.CacheControl;
                if (responseCacheControl?.NoStore == true)
                {
                    try
                    {
                        await _cache.RemoveAsync(cacheKey2, ct);
                    }
                    catch (Exception ex)
                    {
                        // Cache remove failure - ignore
                        _logger.CacheRemoveFailed(request.RequestUri, ex);
                    }
                }
            }

            AddDiagnosticHeaders(uncachedResponse, "MISS-REVALIDATED");
            return uncachedResponse;
        }

        // cachedResponse is null, which means:
        // 1. Cache was empty and factory ran, setting uncachedResponse
        // 2. Factory returned null because response wasn't cacheable
        // Either way, uncachedResponse should be set
        if (uncachedResponse == null)
        {
            // This shouldn't happen, but safety fallback
            uncachedResponse = await base.SendAsync(request, ct);
        }

        AddDiagnosticHeaders(uncachedResponse, "MISS");
        CacheMisses.Add(1);
        return uncachedResponse;
    }

    private CachedHttpMetadata UpdateCachedEntry(CachedHttpMetadata cached, HttpResponseMessage notModifiedResponse)
    {
        // Update metadata from 304 response while keeping the cached body
        var updatedMaxAge = notModifiedResponse.Headers.CacheControl?.MaxAge;
        var updatedExpires = notModifiedResponse.Content.Headers.Expires;
        var updatedDate = notModifiedResponse.Headers.Date;

        // Extract Age from 304 response if present
        TimeSpan? updatedAge = null;
        if (notModifiedResponse.Headers.TryGetValues("Age", out var ageValues))
        {
            var ageValue = ageValues.FirstOrDefault();
            if (ageValue != null && int.TryParse(ageValue, out var ageSeconds))
            {
                updatedAge = TimeSpan.FromSeconds(ageSeconds);
            }
        }

        // Return updated metadata, preserving content reference
        return new CachedHttpMetadata
        {
            StatusCode = cached.StatusCode,
            ContentKey = cached.ContentKey,
            ContentLength = cached.ContentLength,
            Headers = cached.Headers,
            ContentHeaders = cached.ContentHeaders,
            CachedAt = _timeProvider.GetUtcNow(),
            MaxAge = updatedMaxAge ?? cached.MaxAge,
            ETag = cached.ETag,
            LastModified = cached.LastModified,
            Expires = updatedExpires ?? cached.Expires,
            Date = updatedDate ?? cached.Date,
            Age = updatedAge ?? TimeSpan.Zero,
            VaryHeaders = cached.VaryHeaders,
            VaryHeaderValues = cached.VaryHeaderValues,
            StaleWhileRevalidate = cached.StaleWhileRevalidate,
            StaleIfError = cached.StaleIfError,
            MustRevalidate = cached.MustRevalidate,
            NoCache = cached.NoCache,
            IsCompressed = cached.IsCompressed
        };
    }

    private async Task BackgroundRevalidateAsync(
        CachedHttpMetadata cachedResponse,
        HttpRequestMessage originalRequest,
        string cacheKey)
    {
        HttpRequestMessage? revalidationRequest = null;
        try
        {
            revalidationRequest = CreateValidationRequest(originalRequest, cachedResponse);
            var revalidatedResponse = await base.SendAsync(revalidationRequest, Ct.None);

            if (revalidatedResponse.StatusCode == HttpStatusCode.NotModified)
            {
                var updatedEntry = UpdateCachedEntry(cachedResponse, revalidatedResponse);
                try
                {
                    await _cache.SetAsync(cacheKey, updatedEntry, cancellationToken: Ct.None);
                }
                catch (Exception ex)
                {
                    // Cache write failure during background revalidation - ignore
                    _logger.BackgroundCacheWriteFailed(revalidationRequest.RequestUri, ex);
                }
            }
            else
            {
                if (IsResponseCacheable(revalidatedResponse))
                {
                    var freshResponse = await SerializeResponse(revalidatedResponse, revalidationRequest);
                    if (freshResponse != null)
                    {
                        try
                        {
                            await _cache.SetAsync(cacheKey, freshResponse, cancellationToken: Ct.None);
                        }
                        catch (Exception ex)
                        {
                            // Cache write failure during background revalidation - ignore
                            _logger.BackgroundCacheWriteFailed(revalidationRequest.RequestUri, ex);
                        }
                    }
                }
                else
                {
                    var responseCacheControl = revalidatedResponse.Headers.CacheControl;
                    if (responseCacheControl?.NoStore == true)
                    {
                        try
                        {
                            await _cache.RemoveAsync(cacheKey, Ct.None);
                        }
                        catch (Exception ex)
                        {
                            // Cache remove failure - ignore
                            _logger.BackgroundCacheRemoveFailed(revalidationRequest.RequestUri, ex);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Background revalidation failed, keep stale entry
            _logger.BackgroundRevalidationFailed(revalidationRequest?.RequestUri ?? originalRequest.RequestUri, ex);
        }
    }

    private static HttpRequestMessage CreateValidationRequest(
        HttpRequestMessage originalRequest,
        CachedHttpMetadata cachedResponse)
    {
        var request = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri);
        foreach (var header in originalRequest.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(cachedResponse.ETag))
        {
            request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(cachedResponse.ETag));
        }
        else if (cachedResponse.LastModified.HasValue)
        {
            request.Headers.TryAddWithoutValidation(
                "If-Modified-Since",
                cachedResponse.LastModified.Value.ToString("R"));
        }

        return request;
    }

    private string GenerateVaryAwareCacheKey(HttpRequestMessage request)
    {
        // Use custom cache key generator if provided
        if (_options.CacheKeyGenerator != null)
        {
            return _options.CacheKeyGenerator(request);
        }

        var baseCacheKey = $"{request.Method}:{request.RequestUri}";

        // For Vary support: Include configured or default Vary headers in the key
        var potentialVaryHeaders = _options.VaryHeaders ?? new[] { "Accept", "Accept-Encoding", "Accept-Language", "User-Agent" };
        var varyParts = new List<string>();
        foreach (var h in potentialVaryHeaders)
        {
            if (request.Headers.TryGetValues(h, out var values))
            {
                var normalized = string.Join(",", values.Select(v => v.Trim().Replace(" ", "")));
                varyParts.Add($"{h}:{normalized}");
            }
            else
            {
                varyParts.Add($"{h}:");
            }
        }

        var varyKeyPart = string.Join("|", varyParts);
        return $"{baseCacheKey}::{varyKeyPart}";
    }

    private bool IsFresh(CachedHttpMetadata cached, HttpRequestMessage request)
    {
        var freshnessLifetime = CalculateFreshnessLifetime(cached);
        if (freshnessLifetime == null)
        {
            return false;
        }

        var currentAge = CalculateCurrentAge(cached);
        var remainingFreshness = freshnessLifetime.Value - currentAge;

        // RFC 7234 Section 5.2.1.4: min-fresh
        // The min-fresh request directive indicates that the client is willing to
        // accept a response whose freshness lifetime is no less than its current
        // age plus the specified time (in seconds).
        var minFresh = request.Headers.CacheControl?.MinFresh;
        if (minFresh.HasValue)
        {
            // Response must have at least min-fresh seconds of remaining freshness
            return remainingFreshness >= minFresh.Value;
        }

        return currentAge < freshnessLifetime;
    }

    private TimeSpan? CalculateFreshnessLifetime(CachedHttpMetadata cached)
    {
        // Cache mode determines which max-age to prefer
        if (_options.Mode == CacheMode.Shared)
        {
            // Shared cache: Prefer s-maxage (from CacheControl.SharedMaxAge) over max-age
            // Note: MaxAge property may contain s-maxage if it was set during response parsing
            if (cached.MaxAge.HasValue && cached.MaxAge.Value > TimeSpan.Zero)
            {
                return cached.MaxAge.Value;
            }
        }
        else // CacheMode.Private
        {
            // Private cache: Use max-age only (ignore s-maxage)
            if (cached.MaxAge.HasValue && cached.MaxAge.Value > TimeSpan.Zero)
            {
                return cached.MaxAge.Value;
            }
        }

        // Expires header
        if (cached.Expires.HasValue)
        {
            var responseTime = cached.Date ?? cached.CachedAt;
            var lifetime = cached.Expires.Value - responseTime;
            return lifetime > TimeSpan.Zero ? lifetime : TimeSpan.Zero;
        }

        // Heuristic freshness (RFC 7234 Section 4.2.2)
        if (cached.LastModified.HasValue)
        {
            var responseTime = cached.Date ?? cached.CachedAt;
            var timeSinceModified = responseTime - cached.LastModified.Value;
            if (timeSinceModified > TimeSpan.Zero)
            {
                return TimeSpan.FromSeconds(timeSinceModified.TotalSeconds * _options.HeuristicFreshnessPercent);
            }
        }

        return null;
    }

    private TimeSpan CalculateCurrentAge(CachedHttpMetadata cached)
    {
        // Age when received
        var ageValue = cached.Age ?? TimeSpan.Zero;

        // Apparent age based on Date header
        var apparentAge = TimeSpan.Zero;
        if (cached.Date.HasValue)
        {
            apparentAge = cached.CachedAt - cached.Date.Value;
            if (apparentAge < TimeSpan.Zero)
            {
                apparentAge = TimeSpan.Zero;
            }
        }

        var correctedReceivedAge = ageValue > apparentAge ? ageValue : apparentAge;

        // Resident time = time since cached
        var residentTime = _timeProvider.GetUtcNow() - cached.CachedAt;

        return correctedReceivedAge + residentTime;
    }


    private bool IsResponseCacheable(HttpResponseMessage response)
    {
        var responseCacheControl = response.Headers.CacheControl;

        // Don't cache if response has no-store
        if (responseCacheControl?.NoStore == true)
        {
            return false;
        }

        // Shared cache mode: MUST NOT cache responses with private directive
        if (_options.Mode == CacheMode.Shared && responseCacheControl?.Private == true)
        {
            return false;
        }

        // Responses with no-cache can be cached but must be revalidated (RFC 9111 §5.2.2.4)
        // They're cacheable if they have validators, even without explicit freshness
        if (responseCacheControl?.NoCache == true)
        {
            // Can cache if we have validators
            if (response.Headers.ETag != null || response.Content.Headers.LastModified != null)
            {
                return true;
            }
            return false;
        }

        // Don't cache if Vary: * (RFC 7234 §4.1)
        if (response.Headers.TryGetValues("Vary", out var varyValues))
        {
            if (varyValues.Any(v => v.Contains("*")))
            {
                return false;
            }
        }

        // Don't cache if content size exceeds maximum
        if (_options.MaxCacheableContentSize.HasValue && response.Content.Headers.ContentLength.HasValue)
        {
            if (response.Content.Headers.ContentLength.Value > _options.MaxCacheableContentSize.Value)
            {
                return false;
            }
        }

        // Don't cache if content type is not in allowed list
        if (_options.CacheableContentTypes != null)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null)
            {
                return false;
            }

            var isAllowed = _options.CacheableContentTypes.Any(allowed =>
                contentType.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                (allowed.EndsWith("/*") && contentType.StartsWith(allowed[..^2], StringComparison.OrdinalIgnoreCase)));

            if (!isAllowed)
            {
                return false;
            }
        }

        // Check for Cache-Control header with max-age
        if (responseCacheControl?.MaxAge > TimeSpan.Zero)
        {
            return true;
        }

        // Check for Expires header
        if (response.Content.Headers.Expires.HasValue)
        {
            return true;
        }

        // Check for Last-Modified header (allows heuristic freshness)
        if (response.Content.Headers.LastModified.HasValue)
        {
            return true;
        }

        // If default cache duration is configured, response is cacheable
        if (_options.DefaultCacheDuration.HasValue)
        {
            return true;
        }

        return false;
    }

    private async Task<CachedHttpMetadata?> SerializeResponse(HttpResponseMessage response, HttpRequestMessage? request = null)
    {
        // Check content length before reading if available
        if (_options.MaxCacheableContentSize.HasValue &&
            response.Content.Headers.ContentLength.HasValue &&
            response.Content.Headers.ContentLength.Value > _options.MaxCacheableContentSize.Value)
        {
            return null;
        }

        // Capture original content headers before reading
        var originalContentHeaders = response.Content.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToArray()
        );

        // Use SegmentedBuffer to avoid LOH allocations for large responses
        var stream = await response.Content.ReadAsStreamAsync();
        using var segmentedBuffer = new SegmentedBuffer();
        var buffer = ArrayPool<byte>.Shared.Rent(81920); // 80KB buffer

        byte[] finalContent;
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                // Check size limit while reading
                if (_options.MaxCacheableContentSize.HasValue &&
                    segmentedBuffer.Length + bytesRead > _options.MaxCacheableContentSize.Value)
                {
                    // Content too large - restore it so caller can use the response
                    segmentedBuffer.Write(buffer.AsSpan(0, bytesRead));
                    var content = segmentedBuffer.ToArray();
                    response.Content = new ByteArrayContent(content);
                    foreach (var header in originalContentHeaders)
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    return null;
                }
                segmentedBuffer.Write(buffer.AsSpan(0, bytesRead));
            }

            finalContent = segmentedBuffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // Apply compression if enabled and content is large enough
        var isCompressed = false;
        var originalContent = finalContent;
        var contentToCache = finalContent;
        if (_options.CompressionThreshold.HasValue &&
            finalContent.Length >= _options.CompressionThreshold.Value &&
            IsCompressible(response.Content.Headers.ContentType?.MediaType))
        {
            contentToCache = CompressContent(finalContent);
            isCompressed = true;
        }

        var headers = response.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToArray()
        );

        var contentHeaders = originalContentHeaders;

        // Extract cache directives
        var cacheControl = response.Headers.CacheControl;

        // Determine MaxAge based on cache mode
        TimeSpan? maxAge = null;
        if (_options.Mode == CacheMode.Shared)
        {
            // Shared cache: Prefer s-maxage, fallback to max-age
            maxAge = cacheControl?.SharedMaxAge ?? cacheControl?.MaxAge;
        }
        else // CacheMode.Private
        {
            // Private cache: Use max-age only (ignore s-maxage)
            maxAge = cacheControl?.MaxAge;
        }

        // Extract ETag
        var etag = response.Headers.ETag?.Tag;

        // Extract Last-Modified
        var lastModified = response.Content.Headers.LastModified;

        // If no explicit caching headers, use default cache duration
        if (!maxAge.HasValue &&
            !response.Content.Headers.Expires.HasValue &&
            !response.Content.Headers.LastModified.HasValue &&
            _options.DefaultCacheDuration.HasValue)
        {
            maxAge = _options.DefaultCacheDuration;
        }

        // Extract Expires
        var expires = response.Content.Headers.Expires;

        // Extract Date
        var date = response.Headers.Date;

        // Extract Age
        TimeSpan? age = null;
        if (response.Headers.TryGetValues("Age", out var ageValues))
        {
            var ageValue = ageValues.FirstOrDefault();
            if (ageValue != null && int.TryParse(ageValue, out var ageSeconds))
            {
                age = TimeSpan.FromSeconds(ageSeconds);
            }
        }

        // Extract Vary headers and their values from the request
        string[]? varyHeaders = null;
        Dictionary<string, string>? varyHeaderValues = null;

        if (response.Headers.TryGetValues("Vary", out var varyHeaderList))
        {
            // Parse comma-separated Vary header
            varyHeaders = varyHeaderList
                .SelectMany(v => v.Split(','))
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v) && v != "*")
                .ToArray();

            if (varyHeaders.Length > 0 && request != null)
            {
                varyHeaderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var varyHeader in varyHeaders)
                {
                    if (request.Headers.TryGetValues(varyHeader, out var requestHeaderValues))
                    {
                        // Normalize: join multiple values and trim whitespace
                        var normalizedValue = string.Join(",", requestHeaderValues.Select(v => v.Trim()));
                        varyHeaderValues[varyHeader] = normalizedValue;
                    }
                    else
                    {
                        // Header not present in request
                        varyHeaderValues[varyHeader] = string.Empty;
                    }
                }
            }
        }

        // Extract RFC 5861 stale-while-revalidate and stale-if-error
        TimeSpan? staleWhileRevalidate = null;
        TimeSpan? staleIfError = null;
        var mustRevalidate = response.Headers.CacheControl?.MustRevalidate == true;
        var noCache = response.Headers.CacheControl?.NoCache == true;

        // Parse Cache-Control extensions manually since HttpClient doesn't expose them
        if (response.Headers.TryGetValues("Cache-Control", out var cacheControlValues))
        {
            var cacheControlString = string.Join(", ", cacheControlValues);

            // Extract stale-while-revalidate
            var swrMatch = CacheControlRegexes.StaleWhileRevalidate().Match(cacheControlString);
            if (swrMatch.Success && int.TryParse(swrMatch.Groups[1].Value, out var swrSeconds))
            {
                staleWhileRevalidate = TimeSpan.FromSeconds(swrSeconds);
            }

            // Extract stale-if-error
            var sieMatch = CacheControlRegexes.StaleIfError().Match(cacheControlString);
            if (sieMatch.Success && int.TryParse(sieMatch.Groups[1].Value, out var sieSeconds))
            {
                staleIfError = TimeSpan.FromSeconds(sieSeconds);
            }
        }

        // Store content separately (always, to avoid Base64 encoding)
        // Store content first (write order: content before metadata for atomicity)
        var contentKey = await _contentCache.StoreContentAsync(contentToCache, Ct.None);

        // Restore response content so caller can use it (content was consumed during read)
        response.Content = new ByteArrayContent(originalContent);
        foreach (var header in originalContentHeaders)
        {
            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return new CachedHttpMetadata
        {
            StatusCode = (int)response.StatusCode,
            ContentKey = contentKey,
            ContentLength = contentToCache.Length,
            Headers = headers,
            ContentHeaders = contentHeaders,
            CachedAt = _timeProvider.GetUtcNow(),
            MaxAge = maxAge,
            ETag = etag,
            LastModified = lastModified,
            Expires = expires,
            Date = date,
            Age = age,
            VaryHeaders = varyHeaders,
            VaryHeaderValues = varyHeaderValues,
            StaleWhileRevalidate = staleWhileRevalidate,
            StaleIfError = staleIfError,
            MustRevalidate = mustRevalidate,
            NoCache = noCache,
            IsCompressed = isCompressed
        };
    }

    private async Task<HttpResponseMessage?> DeserializeResponseAsync(CachedHttpMetadata metadata, Ct cancellationToken)
    {
        // Get content from separate storage
        var retrievedContent = await _contentCache.GetContentAsync(metadata.ContentKey, cancellationToken);
        if (retrievedContent == null)
        {
            // Content missing - metadata is orphaned
            _logger.LogWarning(
                "Cached content missing for key {ContentKey}, metadata is orphaned",
                metadata.ContentKey);
            return null;
        }

        var content = retrievedContent;

        // Decompress if needed
        if (metadata.IsCompressed)
        {
            content = DecompressContent(content);
        }

        var response = new HttpResponseMessage((HttpStatusCode)metadata.StatusCode)
        {
            Content = new ReadOnlyMemoryContent(content)
        };

        foreach (var header in metadata.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in metadata.ContentHeaders)
        {
            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }

    private bool IsCompressible(string? mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            return false;
        }

        // Use custom list if provided
        if (_options.CompressibleContentTypes != null)
        {
            return _options.CompressibleContentTypes.Any(ct =>
                mediaType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
        }

        // Default compressible types
        return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CompressContent(byte[] content)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzipStream.Write(content, 0, content.Length);
        }
        return outputStream.ToArray();
    }

    private void AddDiagnosticHeaders(HttpResponseMessage response, string reason, CachedHttpMetadata? cachedResponse = null)
    {
        if (!_options.IncludeDiagnosticHeaders)
        {
            return;
        }

        response.Headers.TryAddWithoutValidation("X-Cache-Diagnostic", reason);

        if (cachedResponse != null)
        {
            var age = _timeProvider.GetUtcNow() - cachedResponse.CachedAt;
            response.Headers.TryAddWithoutValidation("X-Cache-Age", $"{(int)age.TotalSeconds}s");

            if (cachedResponse.MaxAge.HasValue)
            {
                response.Headers.TryAddWithoutValidation("X-Cache-MaxAge", $"{(int)cachedResponse.MaxAge.Value.TotalSeconds}s");
            }

            if (cachedResponse.IsCompressed)
            {
                response.Headers.TryAddWithoutValidation("X-Cache-Compressed", "true");
            }
        }
    }

    private static byte[] DecompressContent(byte[] compressedContent)
    {
        using var inputStream = new MemoryStream(compressedContent);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }
}

/// <summary>
/// Source-generated regexes for cache-control parsing.
/// </summary>
internal static partial class CacheControlRegexes
{
    [GeneratedRegex(@"stale-while-revalidate=(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex StaleWhileRevalidate();

    [GeneratedRegex(@"stale-if-error=(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex StaleIfError();
}
