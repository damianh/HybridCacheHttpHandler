// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

namespace DamianH.HybridCacheHttpHandler;

public class NonCacheableTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task POST_requests_not_cached_by_default()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
        };
        mockResponse.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
        var mockHandler = new MockHttpMessageHandler(mockResponse);
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First POST request
        await client.PostAsync("https://example.com/resource", new StringContent("data"), _ct);

        // Second POST request
        await client.PostAsync("https://example.com/resource", new StringContent("data"), _ct);

        mockHandler.RequestCount.ShouldBe(2); // POST not cached
    }

    [Fact]
    public async Task PUT_DELETE_not_cached()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
        };
        mockResponse.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
        var mockHandler = new MockHttpMessageHandler(mockResponse);
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // PUT
        await client.PutAsync("https://example.com/resource", new StringContent("data"));
        await client.PutAsync("https://example.com/resource", new StringContent("data"));

        // DELETE
        await client.DeleteAsync("https://example.com/resource");
        await client.DeleteAsync("https://example.com/resource");

        mockHandler.RequestCount.ShouldBe(4); // None cached
    }

    [Fact]
    public async Task GET_cached()
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

        await client.GetAsync("https://example.com/resource", _ct);
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(1); // GET cached
    }

    [Fact]
    public async Task HEAD_cached_separately_from_GET()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}")
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
            return response;
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // GET request
        await client.GetAsync("https://example.com/resource", _ct);
        await client.GetAsync("https://example.com/resource", _ct);

        requestCount.ShouldBe(1); // Second GET cached

        // HEAD request - should not share cache with GET
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://example.com/resource"));
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://example.com/resource"));

        requestCount.ShouldBe(2); // HEAD cached separately, second HEAD uses cache
    }

    [Fact]
    public async Task Status_200_OK_cacheable()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
        };
        mockResponse.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
        var mockHandler = new MockHttpMessageHandler(mockResponse);
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync("https://example.com/resource", _ct);
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Status_203_204_206_cacheable_with_explicit_freshness()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var statusCodes = new[]
        {
            HttpStatusCode.NonAuthoritativeInformation, // 203
            HttpStatusCode.NoContent, // 204
            HttpStatusCode.PartialContent // 206
        };

        foreach (var statusCode in statusCodes)
        {
            var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("response"),
                Headers = { { "Cache-Control", "max-age=3600" } }
            });
            var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
            var client = new HttpClient(cacheHandler);

            await client.GetAsync($"https://example.com/{statusCode}", _ct);
            await client.GetAsync($"https://example.com/{statusCode}", _ct);

            mockHandler.RequestCount.ShouldBe(1, $"Status {statusCode} should be cacheable with explicit headers");
        }
    }

    [Fact]
    public async Task Status_300_301_308_404_410_cacheable()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var statusCodes = new[]
        {
            HttpStatusCode.MultipleChoices, // 300
            HttpStatusCode.MovedPermanently, // 301
            HttpStatusCode.PermanentRedirect, // 308
            HttpStatusCode.NotFound, // 404
            HttpStatusCode.Gone // 410
        };

        foreach (var statusCode in statusCodes)
        {
            var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("response"),
                Headers = { { "Cache-Control", "max-age=3600" } }
            });
            var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
            var client = new HttpClient(cacheHandler);

            await client.GetAsync($"https://example.com/{statusCode}", _ct);
            await client.GetAsync($"https://example.com/{statusCode}", _ct);

            mockHandler.RequestCount.ShouldBe(1, $"Status {statusCode} should be cacheable");
        }
    }

    [Fact]
    public async Task Other_status_codes_not_cached_without_explicit_headers()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 500 Internal Server Error - not cacheable by default
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("error")
            // No Cache-Control header
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync("https://example.com/resource", _ct);
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2); // Not cached without explicit headers
    }

    [Fact]
    public async Task Authorization_header_requires_Cache_Control_public()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // Without public directive
        var mockResponse1 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
        };
        mockResponse1.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
        var mockHandler1 = new MockHttpMessageHandler(mockResponse1);
        var cacheHandler1 = new HybridCacheHttpHandler(mockHandler1, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client1 = new HttpClient(cacheHandler1);

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Authorization", "Bearer token123");
        await client1.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Authorization", "Bearer token123");
        await client1.SendAsync(request2, _ct);

        mockHandler1.RequestCount.ShouldBe(2); // Not cached without public

        // With public directive
        var mockResponse2 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
        };
        mockResponse2.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromHours(1) };
        var mockHandler2 = new MockHttpMessageHandler(mockResponse2);
        var cacheHandler2 = new HybridCacheHttpHandler(mockHandler2, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client2 = new HttpClient(cacheHandler2);

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource2");
        request3.Headers.Add("Authorization", "Bearer token123");
        await client2.SendAsync(request3, _ct);

        var request4 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource2");
        request4.Headers.Add("Authorization", "Bearer token123");
        await client2.SendAsync(request4, _ct);

        mockHandler2.RequestCount.ShouldBe(1); // Cached with public
    }

    [Fact]
    public async Task Pragma_no_cache_treated_as_Cache_Control_no_cache()
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

        // Second request with Pragma: no-cache
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Pragma", "no-cache");
        await client.SendAsync(request, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Pragma: no-cache bypasses cache
    }
}
