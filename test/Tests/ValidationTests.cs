// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class ValidationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cached_response_with_ETag_triggers_If_None_Match()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var requestCount = 0;
        HttpRequestMessage? lastRequest = null;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                // First request - return response with ETag
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("original content"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1" },
                        { "ETag", "\"123abc\"" }
                    }
                });
            }

            // Second request - capture for assertion
            lastRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - populate cache
        await client.GetAsync("https://example.com/resource", _ct);

        // Make response stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - should trigger validation with If-None-Match
        _ = await client.GetAsync("https://example.com/resource", _ct);

        requestCount.ShouldBe(2);
        lastRequest.ShouldNotBeNull();
        lastRequest.Headers.IfNoneMatch.ShouldContain(etag => etag.Tag == "\"123abc\"");
    }

    [Fact]
    public async Task Response_304_updates_cache_metadata()
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
                    Content = new StringContent("content"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1" },
                        { "ETag", "\"123\"" }
                    }
                };
            }

            // 304 with updated freshness
            return new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Headers =
                {
                    { "Cache-Control", "max-age=3600" }, // Extended freshness
                    { "ETag", "\"123\"" }
                }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance past initial freshness
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - gets 304, updates metadata
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time within new freshness window
        timeProvider.Advance(TimeSpan.FromMinutes(30));

        // Third request - should use cache with updated metadata
        await client.GetAsync("https://example.com/resource", _ct);

        requestCount.ShouldBe(2); // Only 2 requests, third uses refreshed cache
    }

    [Fact]
    public async Task Response_304_returns_cached_body()
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
                    Content = new StringContent("original body"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1" },
                        { "ETag", "\"abc\"" }
                    }
                };
            }
            else
            {
                // 304 has no body
                return new HttpResponseMessage(HttpStatusCode.NotModified)
                {
                    Headers =
                    {
                        { "Cache-Control", "max-age=3600" },
                        { "ETag", "\"abc\"" }
                    }
                };
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        var response1 = await client.GetAsync("https://example.com/resource", _ct);
        var body1 = await response1.Content.ReadAsStringAsync();

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - gets 304 but returns cached body
        var response2 = await client.GetAsync("https://example.com/resource", _ct);
        var body2 = await response2.Content.ReadAsStringAsync();

        body1.ShouldBe("original body");
        body2.ShouldBe("original body"); // Body from cache, not empty 304
        response2.StatusCode.ShouldBe(HttpStatusCode.OK); // Presented as 200 to client
    }

    [Fact]
    public async Task Strong_vs_weak_ETag_comparison()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("content"),
            Headers =
            {
                { "Cache-Control", "max-age=1" },
                { "ETag", "W/\"weak-tag\"" } // Weak ETag
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - should handle weak ETag validation
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2); // Validation attempted
    }

    [Fact]
    public async Task Cached_response_triggers_If_Modified_Since()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var lastModified = timeProvider.GetUtcNow().AddDays(-1);
        var requestCount = 0;
        HttpRequestMessage? lastRequest = null;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("content")
                    {
                        Headers = { LastModified = lastModified }
                    },
                    Headers = { { "Cache-Control", "max-age=1" } }
                });
            }
            else
            {
                lastRequest = req;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)
                {
                    Headers = { { "Cache-Control", "max-age=3600" } }
                });
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - should trigger If-Modified-Since
        await client.GetAsync("https://example.com/resource", _ct);

        requestCount.ShouldBe(2);
        lastRequest.ShouldNotBeNull();
        lastRequest.Headers.IfModifiedSince.ShouldBe(lastModified);
    }

    [Fact]
    public async Task Response_304_updates_cache_entry_date()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var lastModified = timeProvider.GetUtcNow().AddDays(-1);
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("content")
                    {
                        Headers = { LastModified = lastModified }
                    },
                    Headers = { { "Cache-Control", "max-age=1" } }
                };
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.NotModified)
                {
                    Headers = { { "Cache-Control", "max-age=3600" } }
                };
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Validation request
        await client.GetAsync("https://example.com/resource", _ct);

        // Should now be fresh for extended period
        timeProvider.Advance(TimeSpan.FromMinutes(30));
        await client.GetAsync("https://example.com/resource", _ct);

        requestCount.ShouldBe(2); // Third request uses refreshed cache
    }

    [Fact]
    public async Task Last_Modified_fallback_when_no_ETag()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var lastModified = timeProvider.GetUtcNow().AddDays(-1);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("content")
            {
                Headers = { LastModified = lastModified }
            },
            Headers = { { "Cache-Control", "max-age=1" } }
            // No ETag - should use Last-Modified
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - should attempt validation with Last-Modified
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Response_200_replaces_cached_entry()
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
                    Content = new StringContent("old content"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1" },
                        { "ETag", "\"old\"" }
                    }
                };
            }
            else
            {
                // Resource changed - return 200 with new content
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("new content"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=3600" },
                        { "ETag", "\"new\"" }
                    }
                };
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        var response1 = await client.GetAsync("https://example.com/resource", _ct);
        var body1 = await response1.Content.ReadAsStringAsync();

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - resource changed, gets 200 with new content
        var response2 = await client.GetAsync("https://example.com/resource", _ct);
        var body2 = await response2.Content.ReadAsStringAsync();

        body1.ShouldBe("old content");
        body2.ShouldBe("new content");

        // Third request - should use new cached content
        var response3 = await client.GetAsync("https://example.com/resource", _ct);
        var body3 = await response3.Content.ReadAsStringAsync();

        body3.ShouldBe("new content");
        requestCount.ShouldBe(2); // Third uses updated cache
    }

    [Fact]
    public async Task Other_status_codes_handled_correctly()
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
                    Content = new StringContent("content"),
                    Headers =
                    {
                        { "Cache-Control", "max-age=1" },
                        { "ETag", "\"abc\"" }
                    }
                };
            }
            else
            {
                // Validation returns 404 - resource deleted
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Not Found")
                };
            }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Make stale
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second request - validation returns 404
        var response = await client.GetAsync("https://example.com/resource", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        requestCount.ShouldBe(2);
    }
}
