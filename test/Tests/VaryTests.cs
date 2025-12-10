// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class VaryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vary_Accept_creates_separate_cache_entries()
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
                    { "Vary", "Accept" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request with Accept: application/json
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request with same Accept header - should use cache
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept", "application/json");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1); // Second request uses cache

        // Third request with different Accept header - should miss cache
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept", "application/xml");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2); // Different Accept value = cache miss
    }

    [Fact]
    public async Task Vary_Accept_Encoding_handles_multiple_entries()
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
                    { "Vary", "Accept-Encoding" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Request with gzip
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Encoding", "gzip");
        await client.SendAsync(request1, _ct);

        // Request with br
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Encoding", "br");
        await client.SendAsync(request2, _ct);

        // Request with gzip again - should use first cache entry
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept-Encoding", "gzip");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2); // Two unique Accept-Encoding values
    }

    [Fact]
    public async Task Multiple_Vary_headers_supported()
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
                    { "Vary", "Accept, Accept-Language" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        request1.Headers.Add("Accept-Language", "en-US");
        await client.SendAsync(request1, _ct);

        // Same headers - cache hit
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept", "application/json");
        request2.Headers.Add("Accept-Language", "en-US");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1);

        // Different Accept-Language - cache miss
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept", "application/json");
        request3.Headers.Add("Accept-Language", "fr-FR");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Vary_star_makes_response_uncacheable()
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
                { "Vary", "*" } // Wildcard = uncacheable
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request - should not use cache
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2); // Vary: * prevents caching
    }

    [Fact]
    public async Task Missing_Vary_header_in_request_returns_first_match()
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
                    { "Vary", "Accept" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request with Accept header
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request without Accept header - should miss cache
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(2); // Missing header value = cache miss
    }

    [Fact]
    public async Task Case_insensitive_Vary_header_matching()
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
                { "Vary", "Accept" }
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request with different case but same value
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("accept", "application/json"); // Different header name case
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Case-insensitive match
    }

    [Fact]
    public async Task Vary_header_values_are_normalized()
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
                { "Vary", "Accept-Encoding" }
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request with specific order
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        await client.SendAsync(request1, _ct);

        // Second request with same values, different spacing
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Encoding", "gzip,deflate,br");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Should match despite whitespace differences
    }
}
