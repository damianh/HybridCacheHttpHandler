// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class RequestDirectivesTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_with_no_store_bypasses_cache_read()
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
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with no-store - should bypass cache
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "no-store");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Both requests hit origin
    }

    [Fact]
    public async Task Response_not_stored_when_request_has_no_store()
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
        var client = new HttpClient(cacheHandler);

        // Request with no-store
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Cache-Control", "no-store");
        await client.SendAsync(request1, _ct);

        // Second request without no-store - should not find cached entry
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2); // Both requests hit origin
    }

    [Fact]
    public async Task Request_with_no_cache_forces_validation()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "ETag", "\"123\"" }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with no-cache - should force validation
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "no-cache");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Second request triggers validation
        mockHandler.LastRequest.ShouldNotBeNull();
        mockHandler.LastRequest.Headers.IfNoneMatch.ShouldContain(etag => etag.Tag == "\"123\"");
    }

    [Fact]
    public async Task No_cache_sends_conditional_request_even_if_fresh()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "ETag", "\"123\"" }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - cache fresh response
        await client.GetAsync("https://example.com/resource", _ct);
        mockHandler.RequestCount.ShouldBe(1);

        // Second request with no-cache on fresh response
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "no-cache");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Forces revalidation despite freshness
    }

    [Fact]
    public async Task Request_max_age_zero_forces_validation()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "ETag", "\"123\"" }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with max-age=0 - should force validation
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "max-age=0");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Request_max_age_accepts_fresh_responses_within_age()
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
        var client = new HttpClient(cacheHandler);

        // First request - populate cache with 1 hour freshness
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with max-age=7200 (2 hours) - should accept cached
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "max-age=7200");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Cached response used
    }

    [Fact]
    public async Task Only_if_cached_returns_cached_response_if_available()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("cached response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with only-if-cached
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "only-if-cached");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("cached response");
        mockHandler.RequestCount.ShouldBe(1); // No request to origin
    }

    [Fact]
    public async Task Only_if_cached_returns_504_if_not_in_cache()
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
        var client = new HttpClient(cacheHandler);

        // Request with only-if-cached when cache is empty
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "only-if-cached");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout); // 504
        mockHandler.RequestCount.ShouldBe(0); // No request to origin
    }
}

