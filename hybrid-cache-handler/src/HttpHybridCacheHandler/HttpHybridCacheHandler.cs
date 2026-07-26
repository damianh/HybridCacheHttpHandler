// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DamianH.HttpHybridCacheHandler;

/// <summary>
/// An HTTP delegating handler that provides client-side caching based on RFC 9111.
/// </summary>
public class HttpHybridCacheHandler : DelegatingHandler
{
    /// <summary>
    /// Represents the key used to store or retrieve the cache hits counter in a data store or metrics system.
    /// </summary>
    public const string CacheHitsCounterKey = "cache.hits";
    /// <summary>
    /// Represents the key used to identify the cache misses counter in metrics collections.
    /// </summary>
    public const string CacheMissesCounterKey = "cache.misses";
    /// <summary>
    /// Represents the key used to identify the cache stale counter in metrics collections.
    /// </summary>
    public const string CacheStaleCounterKey = "cache.stale";
    /// <summary>
    /// Represents the key used to identify the cache size exceeded counter in metrics collections.
    /// </summary>
    public const string CacheSizeExceededCounterKey = "cache.size_exceeded";

    private readonly HybridCache _cache;
    private readonly ContentCache _contentCache;
    private readonly TimeProvider _timeProvider;
    private readonly HttpHybridCacheHandlerOptions _options;
    private readonly ILogger _logger;
    private static readonly Meter Meter = new(
        "DamianH.HttpHybridCacheHandler",
        typeof(HttpHybridCacheHandler).Assembly.GetName().Version?.ToString() ?? "1.0.0");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(CacheHitsCounterKey, description: "Number of cache hits");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(CacheMissesCounterKey, description: "Number of cache misses");
    private static readonly Counter<long> CacheStale = Meter.CreateCounter<long>(CacheStaleCounterKey, description: "Number of stale cache entries served");
    private static readonly Counter<long> CacheSizeExceeded = Meter.CreateCounter<long>(CacheSizeExceededCounterKey, description: "Number of responses exceeding max cacheable size");

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpHybridCacheHandler"/> class.
    /// </summary>
    /// <param name="cache">The hybrid cache instance to use for caching.</param>
    /// <param name="timeProvider">The time provider for time-based operations. Uses system time if not specified.</param>
    /// <param name="options">Configuration options for the handler. Uses default options if not specified.</param>
    /// <param name="logger">The logger instance. Uses NullLogger if not specified.</param>
    public HttpHybridCacheHandler(
        [FromKeyedServices(ServiceCollectionExtensions.HybridCacheKey)] HybridCache cache,
        TimeProvider timeProvider,
        IOptions<HttpHybridCacheHandlerOptions> options,
        ILogger<HttpHybridCacheHandler> logger)
    {
        _cache = cache;
        _options = options.Value;
        _contentCache = new ContentCache(cache);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpHybridCacheHandler"/> class with a specific inner handler.
    /// </summary>
    /// <param name="innerHandler">The inner handler which is responsible for processing the HTTP response messages.</param>
    /// <param name="cache">The hybrid cache instance to use for caching.</param>
    /// <param name="timeProvider">The time provider for time-based operations. Uses system time if not specified.</param>
    /// <param name="options">Configuration options for the handler. Uses default options if not specified.</param>
    /// <param name="logger">The logger instance. Uses NullLogger if not specified.</param>
    public HttpHybridCacheHandler(
        HttpMessageHandler innerHandler,
        HybridCache cache,
        TimeProvider timeProvider,
        HttpHybridCacheHandlerOptions options,
        ILogger<HttpHybridCacheHandler> logger)
        : base(innerHandler)
    {
        _cache = cache;
        _contentCache = new ContentCache(cache);
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
            await InvalidateCachedResponsesForUnsafeMethodAsync(request, response, ct);
            AddDiagnosticHeaders(response, DiagnosticHeaders.ByPassMethod);
            return response;
        }

        NormalizeIfNoneMatchHeader(request);

        // Check request Cache-Control directives
        var requestCacheControl = request.Headers.CacheControl;

        // Handle only-if-cached
        if (requestCacheControl?.OnlyIfCached == true)
        {
            var cacheKey = GenerateVaryAwareCacheKey(request);

            var cachedEntry = await _cache.GetOrCreateAsync<CachedHttpMetadata?>(
                cacheKey,
                _ => ValueTask.FromResult<CachedHttpMetadata?>(null),
                cancellationToken: ct
            );

            if (cachedEntry != null && MatchesStoredVaryHeaders(cachedEntry, request))
            {
                if (TryCreateConditionalNotModifiedResponse(request, cachedEntry, out var notModifiedResponse))
                {
                    AddDiagnosticHeaders(notModifiedResponse, DiagnosticHeaders.HitNotModified, cachedEntry);
                    return notModifiedResponse;
                }

                var response = await DeserializeResponseAsync(cachedEntry, ct);
                if (response != null)
                {
                    ApplyAgeHeader(response, cachedEntry);
                    AddDiagnosticHeaders(response, DiagnosticHeaders.HitOnlyIfCached, cachedEntry);
                    return response;
                }
                // Content was missing, metadata cleaned up in DeserializeResponseAsync
            }

            // Return 504 Gateway Timeout if not in cache
            var gatewayTimeout = new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                RequestMessage = request
            };
            AddDiagnosticHeaders(gatewayTimeout, DiagnosticHeaders.MissOnlyIfCached);
            return gatewayTimeout;
        }

        // Handle no-store - bypass cache entirely
        if (requestCacheControl?.NoStore == true)
        {
            var response = await base.SendAsync(request, ct);
            AddDiagnosticHeaders(response, DiagnosticHeaders.ByPassNoStore);
            return response;
        }

        // Handle no-cache or max-age=0 - require validation
        var mustRevalidate = requestCacheControl?.NoCache == true
            || requestCacheControl?.MaxAge == TimeSpan.Zero;

        var cacheKey2 = GenerateVaryAwareCacheKey(request);
        var requestUriTag = GetUriTag(request.RequestUri);
        HttpResponseMessage? uncachedResponse = null;
        RawHeaderSnapshot? uncachedRawHeaders = null;

        CachedHttpMetadata? cachedResponse;
        try
        {
            cachedResponse = await _cache.GetOrCreateAsync(
                cacheKey2,
                async cancel =>
                {
                    uncachedResponse = await base.SendAsync(request, cancel);

                    // Snapshot raw headers before any typed header access parses
                    // (and normalizes) them, so responses replay/pass through verbatim.
                    var rawHeaders = uncachedRawHeaders = CaptureRawHeaders(uncachedResponse);

                    // Don't cache if request had no-store
                    if (request.Headers.CacheControl?.NoStore == true)
                    {
                        return null;
                    }

                    // Authorization header handling depends on cache mode
                    if (request.Headers.Authorization != null)
                    {
                        var responseCacheControl = ParseResponseCacheControl(uncachedResponse);

                        if (_options.Mode == CacheMode.Shared)
                        {
                            // Shared cache: Only cache if explicitly marked public or has s-maxage
                            if (!responseCacheControl.Public &&
                                !responseCacheControl.SharedMaxAge.HasValue)
                            {
                                return null;
                            }
                        }
                        else // CacheMode.Private
                        {
                            // Private cache: Require explicit public or private directive for Authorization requests
                            if (!responseCacheControl.Public &&
                                !responseCacheControl.Private)
                            {
                                return null;
                            }
                        }
                    }

                    // Check if response is cacheable
                    if (!IsResponseCacheable(uncachedResponse, request))
                    {
                        return null;
                    }

                    return await SerializeResponse(uncachedResponse, rawHeaders, request);
                },
                tags: requestUriTag == null ? null : [requestUriTag],
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            // Cache read/write failure - fall back to origin
            _logger.CacheOperationFailed(request.RequestUri, ex);
            uncachedResponse ??= await base.SendAsync(request, ct);
            RestoreRawHeaders(uncachedResponse, uncachedRawHeaders);
            AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissCacheError);
            CacheMisses.Add(1, CreateMetricTags(request));
            return uncachedResponse;
        }

        // If we got a cached response, check if it's fresh
        if (cachedResponse != null)
        {
            // If factory just ran (uncachedResponse != null), return the fresh response
            if (uncachedResponse != null)
            {
                // Factory ran, so this is a miss that we just cached.
                // Re-set with accurate TTL since GetOrCreateAsync used the global default.
                try
                {
                    await _cache.SetAsync(cacheKey2, cachedResponse, CreateCacheEntryOptions(cachedResponse), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.CacheWriteFailed(request.RequestUri, ex);
                }
                RestoreRawHeaders(uncachedResponse, uncachedRawHeaders);
                ApplyAgeHeader(uncachedResponse, cachedResponse);
                AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.Miss);
                CacheMisses.Add(1, CreateMetricTags(request));
                return uncachedResponse;
            }

            // From here, uncachedResponse is null, meaning we have a cache hit
            var varyMatches = MatchesStoredVaryHeaders(cachedResponse, request);

            // Check if validation is required (no-cache request or no-cache response)
            if (!varyMatches || mustRevalidate || cachedResponse.NoCache)
            {
                var validationRequest = CreateValidationRequest(request, cachedResponse);
                uncachedResponse = await base.SendAsync(validationRequest, ct);
                var validationRawHeaders = CaptureRawHeaders(uncachedResponse);

                // Handle 304 Not Modified
                if (uncachedResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    var updatedEntry = UpdateCachedEntry(cachedResponse, uncachedResponse);
                    try
                    {
                        await _cache.SetAsync(cacheKey2, updatedEntry, CreateCacheEntryOptions(updatedEntry), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.CacheWriteFailed(request.RequestUri, ex);
                    }

                    CacheHits.Add(1, CreateMetricTags(request));
                    if (TryCreateConditionalNotModifiedResponse(request, updatedEntry, out var notModifiedResponse))
                    {
                        AddDiagnosticHeaders(notModifiedResponse, DiagnosticHeaders.HitNotModified, updatedEntry);
                        return notModifiedResponse;
                    }

                    var response = await DeserializeResponseAsync(updatedEntry, ct);
                    if (response == null)
                    {
                        // Content missing, return fresh response
                        RestoreRawHeaders(uncachedResponse, validationRawHeaders);
                        AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissCacheError);
                        return uncachedResponse;
                    }
                    ApplyAgeHeader(response, updatedEntry);
                    AddDiagnosticHeaders(response, DiagnosticHeaders.HitRevalidated, updatedEntry);
                    return response;
                }

                // Got a new response, cache it
                CacheMisses.Add(1, CreateMetricTags(request));
                RestoreRawHeaders(uncachedResponse, validationRawHeaders);
                AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissRevalidated);
                return uncachedResponse;
            }

            if (IsFresh(cachedResponse, request))
            {
                // Cache hit on fresh response
                CacheHits.Add(1, CreateMetricTags(request));

                if (TryCreateConditionalNotModifiedResponse(request, cachedResponse, out var notModifiedResponse))
                {
                    AddDiagnosticHeaders(notModifiedResponse, DiagnosticHeaders.HitNotModified, cachedResponse);
                    return notModifiedResponse;
                }

                var response = await DeserializeResponseAsync(cachedResponse, ct);
                if (response == null)
                {
                    // Content missing - treat as cache miss
                    await _cache.RemoveAsync(cacheKey2, ct);
                    var freshResponse = await base.SendAsync(request, ct);
                    AddDiagnosticHeaders(freshResponse, DiagnosticHeaders.MissCacheError);
                    CacheMisses.Add(1, CreateMetricTags(request));
                    return freshResponse;
                }
                ApplyAgeHeader(response, cachedResponse);
                AddDiagnosticHeaders(response, DiagnosticHeaders.HitFresh, cachedResponse);
                return response;
            }

            // Response is stale, check stale-while-revalidate
            if (cachedResponse.StaleWhileRevalidate.HasValue)
            {
                var age = CalculateCurrentAge(cachedResponse);
                var freshnessLifetime = CalculateFreshnessLifetime(cachedResponse) ?? TimeSpan.Zero;
                var staleness = age - freshnessLifetime;

                // Within stale-while-revalidate window?
                if (staleness <= cachedResponse.StaleWhileRevalidate.Value)
                {
                    if (TryCreateConditionalNotModifiedResponse(request, cachedResponse, out var notModifiedResponse))
                    {
                        AddDiagnosticHeaders(notModifiedResponse, DiagnosticHeaders.HitNotModified, cachedResponse);

                        // Trigger background revalidation
                        _ = Task.Run(() => BackgroundRevalidateAsync(cachedResponse, request, cacheKey2), ct);

                        CacheHits.Add(1, CreateMetricTags(request)); // Count as hit (stale-while-revalidate)
                        CacheStale.Add(1, CreateMetricTags(request));
                        return notModifiedResponse;
                    }

                    // Serve stale content immediately
                    var staleResponse = await DeserializeResponseAsync(cachedResponse, ct);
                    if (staleResponse == null)
                    {
                        // Content missing - treat as cache miss
                        await _cache.RemoveAsync(cacheKey2, ct);
                        var freshResponse = await base.SendAsync(request, ct);
                        AddDiagnosticHeaders(freshResponse, DiagnosticHeaders.MissCacheError);
                        CacheMisses.Add(1, CreateMetricTags(request));
                        return freshResponse;
                    }
                    ApplyAgeHeader(staleResponse, cachedResponse);
                    AddDiagnosticHeaders(staleResponse, DiagnosticHeaders.HitStaleWhileRevalidate, cachedResponse);

                    // Trigger background revalidation
                    _ = Task.Run(() => BackgroundRevalidateAsync(cachedResponse, request, cacheKey2), ct);

                    CacheHits.Add(1, CreateMetricTags(request)); // Count as hit (stale-while-revalidate)
                    CacheStale.Add(1, CreateMetricTags(request));
                    return staleResponse;
                }
            }

            // Response is stale, attempt validation
            var staleValidationRequest = CreateValidationRequest(request, cachedResponse);

            uncachedResponse = await base.SendAsync(staleValidationRequest, ct);

            // Snapshot raw headers before typed access normalizes them
            var staleValidationRawHeaders = CaptureRawHeaders(uncachedResponse);

            // Check for stale-if-error
            if ((int)uncachedResponse.StatusCode >= 500 &&
                cachedResponse is { StaleIfError: not null, MustRevalidate: false })
            {
                var age = CalculateCurrentAge(cachedResponse);
                var freshnessLifetime = CalculateFreshnessLifetime(cachedResponse) ?? TimeSpan.Zero;
                var staleness = age - freshnessLifetime;

                // Within stale-if-error window?
                if (staleness <= cachedResponse.StaleIfError.Value)
                {
                    CacheHits.Add(1, CreateMetricTags(request)); // Count as hit (stale-if-error)
                    CacheStale.Add(1, CreateMetricTags(request));
                    var response = await DeserializeResponseAsync(cachedResponse, ct);
                    if (response == null)
                    {
                        // Content missing - return error response
                        RestoreRawHeaders(uncachedResponse, staleValidationRawHeaders);
                        AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissCacheError);
                        return uncachedResponse;
                    }
                    ApplyAgeHeader(response, cachedResponse);
                    AddDiagnosticHeaders(response, DiagnosticHeaders.HitStaleIfError, cachedResponse);
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
                    await _cache.SetAsync(cacheKey2, updatedEntry, CreateCacheEntryOptions(updatedEntry), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    // Cache write failure - ignore, still return the response
                    _logger.CacheWriteFailed(request.RequestUri, ex);
                }

                CacheHits.Add(1, CreateMetricTags(request)); // Count as hit (revalidated)
                if (TryCreateConditionalNotModifiedResponse(request, updatedEntry, out var notModifiedResponse))
                {
                    AddDiagnosticHeaders(notModifiedResponse, DiagnosticHeaders.HitNotModified, updatedEntry);
                    return notModifiedResponse;
                }

                // Return cached body with updated metadata
                var response = await DeserializeResponseAsync(updatedEntry, ct);
                if (response == null)
                {
                    // Content missing - return fresh response
                    RestoreRawHeaders(uncachedResponse, staleValidationRawHeaders);
                    AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissCacheError);
                    return uncachedResponse;
                }
                ApplyAgeHeader(response, updatedEntry);
                AddDiagnosticHeaders(response, DiagnosticHeaders.HitRevalidated, updatedEntry);
                return response;
            }

            // Resource changed (200 or other status) - update cache if cacheable
            if (IsResponseCacheable(uncachedResponse, staleValidationRequest))
            {
                var freshResponse = await SerializeResponse(uncachedResponse, staleValidationRawHeaders, staleValidationRequest);
                if (freshResponse != null)
                {
                    try
                    {
                        await _cache.SetAsync(cacheKey2, freshResponse, CreateCacheEntryOptions(freshResponse), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: ct);
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
                var responseCacheControl = ParseResponseCacheControl(uncachedResponse);
                if (responseCacheControl.NoStore)
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

            RestoreRawHeaders(uncachedResponse, staleValidationRawHeaders);
            AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.MissRevalidated);
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
        else
        {
            RestoreRawHeaders(uncachedResponse, uncachedRawHeaders);
        }

        AddDiagnosticHeaders(uncachedResponse, DiagnosticHeaders.Miss);
        CacheMisses.Add(1, CreateMetricTags(request));
        return uncachedResponse;
    }

    private CachedHttpMetadata UpdateCachedEntry(CachedHttpMetadata cached, HttpResponseMessage notModifiedResponse)
    {
        // Update metadata from 304 response while keeping the cached body
        var hasCacheControl = notModifiedResponse.Headers.TryGetValues("Cache-Control", out var cacheControlValues);
        var parsedCacheControl = hasCacheControl
            ? HttpCacheHeaderParser.ParseCacheControl(cacheControlValues!)
            : default;
        var updatedMaxAge = hasCacheControl
            ? _options.Mode == CacheMode.Shared
                ? parsedCacheControl.SharedMaxAge ?? parsedCacheControl.MaxAge
                : parsedCacheControl.MaxAge
            : null;
        var hasExpires = notModifiedResponse.Content.Headers.TryGetValues("Expires", out var expiresValues);
        DateTimeOffset? updatedExpires = hasExpires
            ? HttpCacheHeaderParser.ParseSingleHttpDate(expiresValues!) ?? DateTimeOffset.MinValue
            : null;
        var updatedDate = notModifiedResponse.Headers.Date;
        var currentAge = CalculateCurrentAge(cached);
        var now = _timeProvider.GetUtcNow();

        // Extract Age from 304 response if present
        var updatedAge = notModifiedResponse.Headers.TryGetValues("Age", out var ageValues)
            ? HttpCacheHeaderParser.ParseAge(ageValues)
            : null;
        var ageAfterValidation = updatedAge
            ?? (!updatedDate.HasValue ? currentAge : cached.Age);

        // Return updated metadata, preserving content reference
        return new CachedHttpMetadata
        {
            StatusCode = cached.StatusCode,
            ContentKey = cached.ContentKey,
            ContentLength = cached.ContentLength,
            Headers = cached.Headers,
            ContentHeaders = cached.ContentHeaders,
            CachedAt = now,
            MaxAge = hasCacheControl ? updatedMaxAge : cached.MaxAge,
            ETag = cached.ETag,
            LastModified = cached.LastModified,
            Expires = updatedExpires ?? cached.Expires,
            Date = updatedDate ?? cached.Date,
            Age = ageAfterValidation,
            VaryHeaders = cached.VaryHeaders,
            VaryHeaderValues = cached.VaryHeaderValues,
            StaleWhileRevalidate = hasCacheControl ? parsedCacheControl.StaleWhileRevalidate : cached.StaleWhileRevalidate,
            StaleIfError = hasCacheControl ? parsedCacheControl.StaleIfError : cached.StaleIfError,
            MustRevalidate = hasCacheControl ? parsedCacheControl.MustRevalidate : cached.MustRevalidate,
            NoCache = hasCacheControl ? parsedCacheControl.NoCache : cached.NoCache,
            Public = hasCacheControl ? parsedCacheControl.Public : cached.Public,
            IsCompressed = cached.IsCompressed
        };
    }

    private async Task BackgroundRevalidateAsync(
        CachedHttpMetadata cachedResponse,
        HttpRequestMessage originalRequest,
        string cacheKey)
    {
        var requestUriTag = GetUriTag(originalRequest.RequestUri);
        HttpRequestMessage? revalidationRequest = null;
        try
        {
            revalidationRequest = CreateValidationRequest(originalRequest, cachedResponse);
            var revalidatedResponse = await base.SendAsync(revalidationRequest, Ct.None);

            // Snapshot raw headers before typed access normalizes them
            var revalidatedRawHeaders = CaptureRawHeaders(revalidatedResponse);

            if (revalidatedResponse.StatusCode == HttpStatusCode.NotModified)
            {
                var updatedEntry = UpdateCachedEntry(cachedResponse, revalidatedResponse);
                try
                {
                    await _cache.SetAsync(cacheKey, updatedEntry, CreateCacheEntryOptions(updatedEntry), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: Ct.None);
                }
                catch (Exception ex)
                {
                    // Cache write failure during background revalidation - ignore
                    _logger.BackgroundCacheWriteFailed(revalidationRequest.RequestUri, ex);
                }
            }
            else
            {
                if (IsResponseCacheable(revalidatedResponse, revalidationRequest))
                {
                    var freshResponse = await SerializeResponse(revalidatedResponse, revalidatedRawHeaders, revalidationRequest);
                    if (freshResponse != null)
                    {
                        try
                        {
                            await _cache.SetAsync(cacheKey, freshResponse, CreateCacheEntryOptions(freshResponse), tags: requestUriTag == null ? null : [requestUriTag], cancellationToken: Ct.None);
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
                    var responseCacheControl = ParseResponseCacheControl(revalidatedResponse);
                    if (responseCacheControl.NoStore)
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
            if (header.Key.Equals("If-None-Match", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("If-Modified-Since", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(cachedResponse.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", NormalizeETagForSending(cachedResponse.ETag));
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
        var baseCacheKey = $"{request.Method}:{request.RequestUri}";

        // For Vary support: Include configured or default Vary headers in the key
        var varyParts = new List<string>();
        foreach (var h in _options.VaryHeaders)
        {
            if (request.Headers.TryGetValues(h, out var values))
            {
                var normalized = NormalizeHeaderValues(values);
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

    private static void NormalizeIfNoneMatchHeader(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("If-None-Match", out var ifNoneMatchValues))
        {
            return;
        }

        var normalized = ifNoneMatchValues
            .SelectMany(SplitETagList)
            .Select(NormalizeETagForSending)
            .Where(static value => !string.IsNullOrEmpty(value))
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        request.Headers.Remove("If-None-Match");
        request.Headers.TryAddWithoutValidation("If-None-Match", string.Join(", ", normalized));
    }

    private bool MatchesStoredVaryHeaders(CachedHttpMetadata cachedResponse, HttpRequestMessage request)
    {
        if (cachedResponse.VaryHeaders is not { Length: > 0 })
        {
            return true;
        }

        if (cachedResponse.VaryHeaderValues is null)
        {
            return false;
        }

        foreach (var varyHeader in cachedResponse.VaryHeaders)
        {
            var requestValue = GetNormalizedHeaderValue(request, varyHeader);
            cachedResponse.VaryHeaderValues.TryGetValue(varyHeader, out var cachedValue);
            if (!string.Equals(cachedValue ?? string.Empty, requestValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetNormalizedHeaderValue(HttpRequestMessage request, string headerName)
        => request.Headers.TryGetValues(headerName, out var values)
            ? NormalizeHeaderValues(values)
            : string.Empty;

    private static string NormalizeHeaderValues(IEnumerable<string> values)
        => string.Join(",", values.Select(v => v.Trim().Replace(" ", "", StringComparison.Ordinal)));

    private bool TryCreateConditionalNotModifiedResponse(
        HttpRequestMessage request,
        CachedHttpMetadata cachedResponse,
        out HttpResponseMessage response)
    {
        if (!EvaluateClientConditional(request, cachedResponse))
        {
            response = null!;
            return false;
        }

        response = CreateNotModifiedResponse(request, cachedResponse);
        return true;
    }

    private static bool EvaluateClientConditional(HttpRequestMessage request, CachedHttpMetadata cachedResponse)
    {
        if (request.Headers.TryGetValues("If-None-Match", out var ifNoneMatchValues))
        {
            var storedEtag = cachedResponse.ETag;
            foreach (var candidate in ifNoneMatchValues.SelectMany(SplitETagList))
            {
                if (candidate == "*")
                {
                    return true;
                }

                if (string.IsNullOrEmpty(storedEtag))
                {
                    continue;
                }

                if (WeakEntityTagEquals(candidate, storedEtag))
                {
                    return true;
                }
            }

            return false;
        }

        if (!request.Headers.TryGetValues("If-Modified-Since", out var ifModifiedSinceValues))
        {
            return false;
        }

        var ifModifiedSince = ifModifiedSinceValues.FirstOrDefault();
        if (!TryParseHttpDate(ifModifiedSince, out var ifModifiedSinceDate))
        {
            return false;
        }

        if (!cachedResponse.LastModified.HasValue)
        {
            return true;
        }

        return cachedResponse.LastModified.Value <= ifModifiedSinceDate;
    }

    private static HttpResponseMessage CreateNotModifiedResponse(HttpRequestMessage request, CachedHttpMetadata cachedResponse)
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotModified)
        {
            RequestMessage = request,
            Content = new ByteArrayContent([])
        };

        foreach (var header in cachedResponse.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in cachedResponse.ContentHeaders)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content.Headers.Remove("Content-Length");
        return response;
    }

    private static bool TryParseHttpDate(string? value, out DateTimeOffset parsed)
        => DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed);

    private static bool WeakEntityTagEquals(string left, string right)
        => string.Equals(
            NormalizeETagForComparison(left),
            NormalizeETagForComparison(right),
            StringComparison.Ordinal);

    private static string NormalizeETagForComparison(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return normalized;
        }

        if (normalized.Length >= 2 &&
            (normalized[0] == 'W' || normalized[0] == 'w'))
        {
            if (normalized[1] == '/' || normalized[1] == '\\')
            {
                normalized = normalized[2..];
            }
            else if (normalized[1] == '"')
            {
                normalized = normalized[1..];
            }
        }

        normalized = normalized.Trim();
        if (normalized.Length >= 2 &&
            normalized[0] == '"' &&
            normalized[^1] == '"')
        {
            normalized = normalized[1..^1];
        }

        return normalized;
    }

    private static string NormalizeETagForSending(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized == "*")
        {
            return normalized;
        }

        if ((normalized[0] == 'W' || normalized[0] == 'w') && normalized.Length >= 2)
        {
            if (normalized[1] == '/' || normalized[1] == '\\' || normalized[1] == '"')
            {
                return normalized;
            }
        }

        if (normalized.Length >= 2 &&
            normalized[0] == '"' &&
            normalized[^1] == '"')
        {
            return normalized;
        }

        return $"\"{normalized}\"";
    }

    private static IEnumerable<string> SplitETagList(string value)
    {
        var builder = new StringBuilder();
        var inQuotes = false;
        var previous = '\0';

        foreach (var character in value)
        {
            if (character == '"' && previous != '\\')
            {
                inQuotes = !inQuotes;
            }

            if (character == ',' && !inQuotes)
            {
                var etag = builder.ToString().Trim();
                if (etag.Length > 0)
                {
                    yield return etag;
                }

                builder.Clear();
                previous = character;
                continue;
            }

            builder.Append(character);
            previous = character;
        }

        var final = builder.ToString().Trim();
        if (final.Length > 0)
        {
            yield return final;
        }
    }

    private async Task InvalidateCachedResponsesForUnsafeMethodAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        Ct ct)
    {
        if (!IsUnsafeMethod(request.Method)
            || !IsNonErrorStatus(response.StatusCode)
            || request.RequestUri == null)
        {
            return;
        }

        var targetUri = request.RequestUri;
        var urisToInvalidate = new HashSet<Uri>();
        urisToInvalidate.Add(targetUri);

        var locationUri = ResolveSameOriginUri(targetUri, response.Headers.Location);
        if (locationUri != null)
        {
            urisToInvalidate.Add(locationUri);
        }

        var contentLocationUri = ResolveSameOriginUri(targetUri, response.Content?.Headers.ContentLocation);
        if (contentLocationUri != null)
        {
            urisToInvalidate.Add(contentLocationUri);
        }

        foreach (var uri in urisToInvalidate)
        {
            var uriTag = GetUriTag(uri);
            if (uriTag == null)
            {
                continue;
            }

            try
            {
                await _cache.RemoveByTagAsync(uriTag, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.CacheInvalidationFailed(uri, ex);
            }
        }
    }

    private static bool IsUnsafeMethod(HttpMethod method) =>
        method != HttpMethod.Get
        && method != HttpMethod.Head
        && method != HttpMethod.Options
        && method != HttpMethod.Trace;

    private static bool IsNonErrorStatus(HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and < 400;

    private static Uri? ResolveSameOriginUri(Uri requestUri, Uri? candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        var resolvedUri = candidate.IsAbsoluteUri ? candidate : new Uri(requestUri, candidate);
        return IsSameOrigin(requestUri, resolvedUri) ? resolvedUri : null;
    }

    private static bool IsSameOrigin(Uri first, Uri second) =>
        string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase)
        && first.Port == second.Port;

    private static string? GetUriTag(Uri? uri)
    {
        if (uri == null || !uri.IsAbsoluteUri)
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };
        return $"httpcache:uri:{builder.Uri.AbsoluteUri}";
    }

    private bool IsFresh(CachedHttpMetadata cached, HttpRequestMessage request)
    {
        var freshnessLifetime = CalculateFreshnessLifetime(cached);
        if (freshnessLifetime == null)
        {
            return false;
        }

        var currentAge = CalculateCurrentAge(cached);
        var requestCacheControl = request.Headers.CacheControl;

        // RFC 9111 Section 5.2.1.1: max-age request directive
        if (requestCacheControl?.MaxAge is TimeSpan requestMaxAge && currentAge > requestMaxAge)
        {
            return false;
        }

        var remainingFreshness = freshnessLifetime.Value - currentAge;

        // RFC 7234 Section 5.2.1.4: min-fresh
        // The min-fresh request directive indicates that the client is willing to
        // accept a response whose freshness lifetime is no less than its current
        // age plus the specified time (in seconds).
        var minFresh = requestCacheControl?.MinFresh;
        if (minFresh.HasValue)
        {
            // Response must have at least min-fresh seconds of remaining freshness
            if (remainingFreshness < minFresh.Value)
            {
                return false;
            }
        }

        if (currentAge < freshnessLifetime)
        {
            return true;
        }

        // RFC 9111 Section 5.2.1.2: max-stale request directive
        if (requestCacheControl?.MaxStale == true &&
            !cached.MustRevalidate &&
            !cached.NoCache)
        {
            var staleness = currentAge - freshnessLifetime.Value;
            var maxStaleLimit = requestCacheControl.MaxStaleLimit ?? TimeSpan.MaxValue;
            return staleness <= maxStaleLimit;
        }

        return false;
    }

    private TimeSpan? CalculateFreshnessLifetime(CachedHttpMetadata cached)
    {
        // Cache mode determines which max-age to prefer
        if (_options.Mode == CacheMode.Shared)
        {
            // Shared cache: Prefer s-maxage (from CacheControl.SharedMaxAge) over max-age
            // Note: MaxAge property may contain s-maxage if it was set during response parsing
            if (cached.MaxAge.HasValue)
            {
                return cached.MaxAge.Value;
            }
        }
        else // CacheMode.Private
        {
            // Private cache: Use max-age only (ignore s-maxage)
            if (cached.MaxAge.HasValue)
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
                var heuristicLifetime = TimeSpan.FromSeconds(timeSinceModified.TotalSeconds * _options.HeuristicFreshnessPercent);
                return heuristicLifetime < _options.HeuristicFreshnessMinimum
                    ? _options.HeuristicFreshnessMinimum
                    : heuristicLifetime;
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

    /// <summary>
    /// Computes <see cref="HybridCacheEntryOptions"/> from cached metadata so that
    /// HybridCache evicts the entry at approximately the same time the handler would
    /// consider it unusable. The TTL encompasses freshness lifetime plus any
    /// stale-while-revalidate and stale-if-error windows.
    /// </summary>
    private HybridCacheEntryOptions CreateCacheEntryOptions(CachedHttpMetadata metadata)
    {
        var freshness = CalculateFreshnessLifetime(metadata) ?? TimeSpan.Zero;

        // Add stale extension windows so entries survive long enough for the handler
        // to serve stale responses when appropriate.
        var total = freshness;
        if (metadata.StaleWhileRevalidate.HasValue)
        {
            total += metadata.StaleWhileRevalidate.Value;
        }
        if (metadata.StaleIfError.HasValue)
        {
            total += metadata.StaleIfError.Value;
        }

        // Ensure a minimum TTL so that very short-lived entries don't disappear
        // before the handler can check freshness on the next request.
        if (total < TimeSpan.FromSeconds(30))
        {
            total = TimeSpan.FromSeconds(30);
        }

        return new HybridCacheEntryOptions
        {
            Expiration = total,
            LocalCacheExpiration = total
        };
    }

    private bool IsResponseCacheable(HttpResponseMessage response, HttpRequestMessage? request = null)
    {
        var responseCacheControl = ParseResponseCacheControl(response);

        // Don't cache if response has no-store
        if (responseCacheControl.NoStore)
        {
            return false;
        }

        // Shared cache mode: MUST NOT cache responses with private directive
        if (_options.Mode == CacheMode.Shared && responseCacheControl.Private)
        {
            return false;
        }

        // Responses with no-cache can be cached but must be revalidated (RFC 9111 §5.2.2.4)
        // They're cacheable if they have validators, even without explicit freshness
        if (responseCacheControl.NoCache)
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
        if (response.Content.Headers.ContentLength.HasValue)
        {
            if (response.Content.Headers.ContentLength.Value >= _options.MaxCacheableContentSize)
            {
                if (request != null)
                {
                    CacheSizeExceeded.Add(1, CreateMetricTags(request));
                }
                return false;
            }
        }

        // Don't cache if content type is not in allowed list
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

        // Check for Cache-Control header with max-age
        if (_options.Mode == CacheMode.Shared)
        {
            if (responseCacheControl.SharedMaxAge.HasValue || responseCacheControl.MaxAge.HasValue)
            {
                return true;
            }
        }
        else if (responseCacheControl.MaxAge.HasValue)
        {
            return true;
        }

        // Check for Expires header
        if (response.Content.Headers.TryGetValues("Expires", out _))
        {
            return true;
        }

        // Check for Last-Modified header (allows heuristic freshness)
        if (response.Content.Headers.LastModified.HasValue)
        {
            return IsHeuristicallyCacheableStatus(response.StatusCode) || responseCacheControl.Public;
        }

        // If default cache duration is configured, response is cacheable
        if (_options.FallbackCacheDuration > TimeSpan.MinValue)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Snapshots raw (unparsed) header values. Must be called immediately after a
    /// response is received, before any typed header access (e.g. Headers.CacheControl)
    /// parses the raw values — parsed values re-serialize reordered/case-normalized,
    /// and RFC 9111 requires stored header field values to be replayed unchanged.
    /// </summary>
    private static RawHeaderSnapshot CaptureRawHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string[]>();
        foreach (var header in response.Headers.NonValidated)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        var contentHeaders = new Dictionary<string, string[]>();
        foreach (var header in response.Content.Headers.NonValidated)
        {
            contentHeaders[header.Key] = header.Value.ToArray();
        }

        return new RawHeaderSnapshot(headers, contentHeaders);
    }

    private sealed record RawHeaderSnapshot(
        Dictionary<string, string[]> Headers,
        Dictionary<string, string[]> ContentHeaders);

    /// <summary>
    /// Restores raw header values captured by <see cref="CaptureRawHeaders"/> onto a
    /// response whose headers may have been parsed (and thus re-serialized normalized)
    /// by typed header access, so the response is passed through verbatim.
    /// </summary>
    private static void RestoreRawHeaders(HttpResponseMessage response, RawHeaderSnapshot? rawHeaders)
    {
        if (rawHeaders == null)
        {
            return;
        }

        response.Headers.Clear();
        foreach (var header in rawHeaders.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content.Headers.Clear();
        foreach (var header in rawHeaders.ContentHeaders)
        {
            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private async Task<CachedHttpMetadata?> SerializeResponse(HttpResponseMessage response, RawHeaderSnapshot rawHeaders, HttpRequestMessage? request = null)
    {
        // Check content length before reading if available
        if (response.Content.Headers.ContentLength.HasValue &&
            response.Content.Headers.ContentLength.Value > _options.MaxCacheableContentSize)
        {
            if (request != null)
            {
                CacheSizeExceeded.Add(1, CreateMetricTags(request));
            }
            return null;
        }

        // Raw content headers, captured before reading/replacing the content
        var originalContentHeaders = rawHeaders.ContentHeaders;

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
                if (segmentedBuffer.Length + bytesRead > _options.MaxCacheableContentSize)
                {
                    // Content too large - restore it so caller can use the response
                    segmentedBuffer.Write(buffer.AsSpan(0, bytesRead));
                    var content = segmentedBuffer.ToArray();
                    response.Content = new ByteArrayContent(content);
                    foreach (var header in originalContentHeaders)
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    if (request != null)
                    {
                        CacheSizeExceeded.Add(1, CreateMetricTags(request));
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
        if (_options.CompressionThreshold > 0 &&
            finalContent.Length >= _options.CompressionThreshold &&
            IsCompressible(response.Content.Headers.ContentType?.MediaType))
        {
            contentToCache = CompressContent(finalContent);
            isCompressed = true;
        }

        var headers = rawHeaders.Headers;

        var contentHeaders = originalContentHeaders;

        // Extract cache directives
        var parsedCacheControl = response.Headers.TryGetValues("Cache-Control", out var cacheControlValues)
            ? HttpCacheHeaderParser.ParseCacheControl(cacheControlValues)
            : default;

        // Determine MaxAge based on cache mode
        TimeSpan? maxAge = null;
        if (_options.Mode == CacheMode.Shared)
        {
            // Shared cache: Prefer s-maxage, fallback to max-age
            maxAge = parsedCacheControl.SharedMaxAge ?? parsedCacheControl.MaxAge;
        }
        else // CacheMode.Private
        {
            // Private cache: Use max-age only (ignore s-maxage)
            maxAge = parsedCacheControl.MaxAge;
        }

        // Extract ETag
        string? etag = null;
        if (response.Headers.TryGetValues("ETag", out var etagValues))
        {
            etag = etagValues.FirstOrDefault();
        }

        // Extract Last-Modified
        var lastModified = response.Content.Headers.LastModified;

        // Extract Expires using strict HTTP-date parsing
        DateTimeOffset? expires = null;
        var hasExpires = response.Content.Headers.TryGetValues("Expires", out var expiresValues);
        if (hasExpires)
        {
            expires = HttpCacheHeaderParser.ParseSingleHttpDate(expiresValues!) ?? DateTimeOffset.MinValue;
        }

        // If no explicit caching headers, use default cache duration
        if (!maxAge.HasValue &&
            !hasExpires &&
            !response.Content.Headers.LastModified.HasValue &&
            _options.FallbackCacheDuration > TimeSpan.MinValue)
        {
            maxAge = _options.FallbackCacheDuration;
        }

        // Extract Date
        var date = response.Headers.Date;

        // Extract Age
        var age = response.Headers.TryGetValues("Age", out var ageValues)
            ? HttpCacheHeaderParser.ParseAge(ageValues)
            : null;

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
                        var normalizedValue = NormalizeHeaderValues(requestHeaderValues);
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
        var staleWhileRevalidate = parsedCacheControl.StaleWhileRevalidate;
        var staleIfError = parsedCacheControl.StaleIfError;
        var mustRevalidate = parsedCacheControl.MustRevalidate;
        var noCache = parsedCacheControl.NoCache;

        // Store content separately (always, to avoid Base64 encoding)
        // Store content first (write order: content before metadata for atomicity)
        var requestUriTag = GetUriTag(request?.RequestUri);
        IEnumerable<string>? contentTags = requestUriTag == null ? null : [requestUriTag];
        var contentKey = await _contentCache.StoreContentAsync(contentToCache, null, contentTags, Ct.None);

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
            Public = parsedCacheControl.Public,
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
            _logger.CachedContentMissing(metadata.ContentKey);
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

        return _options.CompressibleContentTypes.Any(contentType =>
            contentType.EndsWith("/*")
                ? mediaType.StartsWith(contentType[..^2], StringComparison.OrdinalIgnoreCase)
                : mediaType.StartsWith(contentType, StringComparison.OrdinalIgnoreCase));
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

    private static HttpCacheHeaderParser.CacheControlDirectives ParseResponseCacheControl(HttpResponseMessage response)
        => response.Headers.TryGetValues("Cache-Control", out var values)
            ? HttpCacheHeaderParser.ParseCacheControl(values)
            : default;

    private void ApplyAgeHeader(HttpResponseMessage response, CachedHttpMetadata cachedResponse)
    {
        var currentAge = CalculateCurrentAge(cachedResponse);
        var ageSeconds = Math.Max(0L, (long)Math.Floor(currentAge.TotalSeconds));
        response.Headers.Remove("Age");
        response.Headers.TryAddWithoutValidation("Age", ageSeconds.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsHeuristicallyCacheableStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.NonAuthoritativeInformation => true,
            HttpStatusCode.NoContent => true,
            HttpStatusCode.PartialContent => true,
            HttpStatusCode.MultipleChoices => true,
            HttpStatusCode.MovedPermanently => true,
            HttpStatusCode.PermanentRedirect => true,
            HttpStatusCode.NotFound => true,
            HttpStatusCode.MethodNotAllowed => true,
            HttpStatusCode.Gone => true,
            HttpStatusCode.RequestUriTooLong => true,
            HttpStatusCode.NotImplemented => true,
            _ => false
        };

    private void AddDiagnosticHeaders(HttpResponseMessage response, string reason, CachedHttpMetadata? cachedResponse = null)
    {
        if (!_options.IncludeDiagnosticHeaders)
        {
            return;
        }

        response.Headers.TryAddWithoutValidation(DiagnosticHeaders.CacheDiagnostic, reason);

        if (cachedResponse != null)
        {
            var age = _timeProvider.GetUtcNow() - cachedResponse.CachedAt;
            response.Headers.TryAddWithoutValidation(DiagnosticHeaders.CacheAge, $"{(int)age.TotalSeconds}s");

            if (cachedResponse.MaxAge.HasValue)
            {
                response.Headers.TryAddWithoutValidation(DiagnosticHeaders.CacheMaxAge, $"{(int)cachedResponse.MaxAge.Value.TotalSeconds}s");
            }

            if (cachedResponse.IsCompressed)
            {
                response.Headers.TryAddWithoutValidation(DiagnosticHeaders.CacheCompressed, "true");
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

    private static TagList CreateMetricTags(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        return new TagList
        {
            { "http.request.method", request.Method.Method },
            { "url.scheme", uri?.Scheme ?? "unknown" },
            { "server.address", uri?.Host ?? "unknown" },
            { "server.port", uri?.Port ?? 0 }
        };
    }
}
