// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Caching.Hybrid;

namespace DamianH.HttpHybridCacheHandler;

public class RequestDirectivesTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_with_no_store_bypasses_cache_read()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

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
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

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
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

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
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

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
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

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
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        // First request - populate cache with 1 hour freshness
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with max-age=7200 (2 hours) - should accept cached
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "max-age=7200");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Cached response used
    }

    [Fact]
    public async Task Request_max_age_rejects_response_older_than_requested_limit()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Age", "1800" }
            }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        // First request - populate cache with a response already 30 minutes old
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request asks for max-age=1 second; cached response should be rejected
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "max-age=1");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Request_max_stale_allows_serving_stale_response_within_limit()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=1" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Make response stale
        fixture.AdvanceTime(TimeSpan.FromSeconds(2));

        // max-stale should allow using stale cache entry
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "max-stale=1000");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Only_if_cached_returns_cached_response_if_available()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("cached response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request with only-if-cached
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "only-if-cached");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(_ct);
        content.ShouldBe("cached response");
        mockHandler.RequestCount.ShouldBe(1); // No request to origin
    }

    [Fact]
    public async Task Only_if_cached_returns_504_if_not_in_cache()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        // Request with only-if-cached when cache is empty
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "only-if-cached");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout); // 504
        mockHandler.RequestCount.ShouldBe(0); // No request to origin
    }
    [Fact]
    public async Task Only_if_cached_returns_504_when_cache_read_fails()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            customCache: new ThrowOnGetCache());
        using var client = fixture.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Cache-Control", "only-if-cached");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        mockHandler.RequestCount.ShouldBe(0);
    }

    private sealed class ThrowOnGetCache : HybridCache
    {
        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, Ct, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default) =>
            throw new InvalidOperationException("Simulated cache read failure");

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(string key, Ct cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(IEnumerable<string> keys, Ct cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(string tag, Ct cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, Ct cancellationToken = default) => ValueTask.CompletedTask;
    }
}
