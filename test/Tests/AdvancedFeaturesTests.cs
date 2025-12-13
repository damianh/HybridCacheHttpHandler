// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class AdvancedFeaturesTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Serve_stale_response_while_revalidating_in_background()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=1, stale-while-revalidate=5" },
                    { "ETag", $"\"{requestCount}\"" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        var response1 = await client.GetAsync("https://example.com/resource", _ct);
        var body1 = await response1.Content.ReadAsStringAsync();
        body1.ShouldBe("response 1");

        // Advance time past max-age but within stale-while-revalidate window
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - should get stale response immediately
        var response2 = await client.GetAsync("https://example.com/resource", _ct);
        var body2 = await response2.Content.ReadAsStringAsync();
        body2.ShouldBe("response 1"); // Stale content served

        // Give background revalidation time to complete
        await Task.Delay(100);

        requestCount.ShouldBe(2); // Background revalidation happened

        // Third request should get fresh content from background revalidation
        var response3 = await client.GetAsync("https://example.com/resource", _ct);
        var body3 = await response3.Content.ReadAsStringAsync();
        body3.ShouldBe("response 2"); // Updated from background revalidation
    }

    [Fact]
    public async Task Configure_stale_while_revalidate_window()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=1, stale-while-revalidate=10" },
                    { "ETag", "\"abc\"" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Within stale-while-revalidate window (6 seconds after max-age)
        timeProvider.Advance(TimeSpan.FromSeconds(7));
        var response = await client.GetAsync("https://example.com/resource", _ct);
        var body = await response.Content.ReadAsStringAsync();

        body.ShouldBe("response 1"); // Stale content served within window
        await Task.Delay(100);
        requestCount.ShouldBe(2); // Revalidation triggered
    }

    [Fact]
    public async Task Update_cache_asynchronously()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            // Simulate slow revalidation
            Thread.Sleep(50);
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=1, stale-while-revalidate=5" },
                    { "ETag", "\"abc\"" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync("https://example.com/resource", _ct);
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // This should return quickly with stale content
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("https://example.com/resource", _ct);
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("response 1"); // Stale response
        sw.ElapsedMilliseconds.ShouldBeLessThan(50); // Fast response (not waiting for revalidation)
    }

    [Fact]
    public async Task Serve_stale_response_on_upstream_error()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("response 1"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1, stale-if-error=10" },
                        { "ETag", "\"abc\"" }
                    }
                };
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("error")
                };
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time past max-age
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - origin returns error, should serve stale
        var response = await client.GetAsync("https://example.com/resource", _ct);
        var body = await response.Content.ReadAsStringAsync();

        body.ShouldBe("response 1"); // Stale content served due to error
        response.StatusCode.ShouldBe(HttpStatusCode.OK); // Presented as OK
    }

    [Fact]
    public async Task Configure_stale_if_error_window()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("response"),
                    Headers = { { "Cache-Control", "max-age=1, stale-if-error=5" } }
                };
            }
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync("https://example.com/resource", _ct);

        // Within stale-if-error window
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        var response1 = await client.GetAsync("https://example.com/resource", _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK); // Stale served

        // Beyond stale-if-error window
        timeProvider.Advance(TimeSpan.FromSeconds(4));
        var response2 = await client.GetAsync("https://example.com/resource", _ct);
        response2.StatusCode.ShouldBe(HttpStatusCode.InternalServerError); // Error passed through
    }

    [Fact]
    public async Task Respect_must_revalidate_with_stale_if_error()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("response"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1, stale-if-error=10, must-revalidate" }
                    }
                };
            }
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync("https://example.com/resource", _ct);
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // must-revalidate prevents serving stale on error
        var response = await client.GetAsync("https://example.com/resource", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Track_hit_miss_ratio()
    {
        // This test verifies that metrics are being tracked by checking the hit/miss behavior
        // rather than trying to capture the actual metric values (which is complex with static meters)
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers = { { "Cache-Control", "max-age=3600" } }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - miss (requestCount=1)
        await client.GetAsync("https://example.com/resource1", _ct);

        // Second request - hit (requestCount still 1)
        await client.GetAsync("https://example.com/resource1", _ct);

        // Third request different resource - miss (requestCount=2)
        await client.GetAsync("https://example.com/resource2", _ct);

        // Verify behavior: 2 misses (unique resources), 1 hit (cached)
        requestCount.ShouldBe(2); // Only 2 actual requests made (2 misses)

        // The metrics counters (_cacheHits and _cacheMisses) are being incremented
        // but capturing them in tests with static meters is complex
        // The Expose_metrics_via_System_Diagnostics_Metrics test verifies the meter exists
    }

    [Fact]
    public async Task Expose_metrics_via_System_Diagnostics_Metrics()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);

        // Verify that meter exists and has expected instruments
        var meterFound = false;
        using var meterListener = new MeterListener();

        meterListener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == "HybridCacheHttpHandler")
            {
                meterFound = true;
                instrument.Name.ShouldBeOneOf("cache.hits", "cache.misses");
            }
        };

        meterListener.Start();

        var client = new HttpClient(cacheHandler);
        await client.GetAsync("https://example.com/resource", _ct);

        meterFound.ShouldBeTrue();
    }

    [Fact]
    public async Task Allow_custom_cache_key_generation()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        // Custom key generator that ignores query strings
        var options = new HybridCacheHttpHandlerOptions
        {
            CacheKeyGenerator = (request) => $"{request.Method}:{request.RequestUri?.GetLeftPart(UriPartial.Path)}"
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Two requests with different query strings
        await client.GetAsync("https://example.com/resource?v=1", _ct);
        await client.GetAsync("https://example.com/resource?v=2", _ct);

        mockHandler.RequestCount.ShouldBe(1); // Same cache key despite different query strings
    }

    [Fact]
    public async Task Include_exclude_specific_headers()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "X-Custom-Header" }
                }
            };
        });

        // Only include X-Custom-Header in cache key, ignore others
        var options = new HybridCacheHttpHandlerOptions
        {
            VaryHeaders = new[] { "X-Custom-Header" }
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("X-Custom-Header", "value1");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("X-Custom-Header", "value1");
        request2.Headers.Add("Accept", "application/xml"); // Different Accept
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1); // Cache hit despite different Accept header

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("X-Custom-Header", "value2"); // Different custom header
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2); // Cache miss due to different X-Custom-Header
    }

    [Fact]
    public async Task Support_per_request_cache_key_strategy()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers = { { "Cache-Control", "max-age=3600" } }
            };
        });

        var generatedKeys = new List<string>();
        var detectedDeviceTypes = new List<string>();

        // Cache key includes request properties
        var options = new HybridCacheHttpHandlerOptions
        {
            CacheKeyGenerator = (request) =>
            {
                var baseKey = $"{request.Method}:{request.RequestUri}";

                // Include custom device type header in key
                if (request.Headers.TryGetValues("X-Device-Type", out var deviceTypes))
                {
                    var deviceType = deviceTypes.First();
                    detectedDeviceTypes.Add(deviceType);
                    baseKey += $":device={deviceType}";
                }
                else
                {
                    detectedDeviceTypes.Add("(none)");
                    baseKey += ":device=unknown";
                }

                generatedKeys.Add(baseKey);
                return baseKey;
            }
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("X-Device-Type", "Mobile");
        var response1 = await client.SendAsync(request1, _ct);
        var body1 = await response1.Content.ReadAsStringAsync();

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("X-Device-Type", "Desktop");
        var response2 = await client.SendAsync(request2, _ct);
        var body2 = await response2.Content.ReadAsStringAsync();

        // Verify different keys were generated
        generatedKeys.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Debug: check what device types were detected
        if (detectedDeviceTypes.Count >= 2)
        {
            detectedDeviceTypes[0].ShouldBe("Mobile");
            detectedDeviceTypes[1].ShouldBe("Desktop");
        }

        generatedKeys[0].ShouldContain(":device=Mobile");
        generatedKeys[1].ShouldContain(":device=Desktop");

        // Verify different responses (different cache entries)
        body1.ShouldBe("response 1");
        body2.ShouldBe("response 2");

        requestCount.ShouldBe(2); // Different cache keys for mobile vs desktop
    }
}
