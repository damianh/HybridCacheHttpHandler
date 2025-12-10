// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class ContentSizeLimitTests
{
    private const string TestUrl = "https://example.com/resource";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Response_within_size_limit_is_cached()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 1 KB content
        var content = new string('x', 1024);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 10 * 1024 // 10 KB
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Second request was cached
    }

    [Fact]
    public async Task Response_exceeding_size_limit_by_ContentLength_is_not_cached()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 20 KB content (exceeds 10 KB limit)
        var content = new string('x', 20 * 1024);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 10 * 1024 // 10 KB
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Not cached due to size
    }

    [Fact]
    public async Task Default_max_size_is_10MB()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 5 MB content (within default 10 MB limit)
        var content = new byte[5 * 1024 * 1024];
        Array.Fill(content, (byte)'x');

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new ByteArrayContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Cached with default limit
    }

    [Fact]
    public async Task Response_exceeding_11MB_with_default_limit_is_not_cached()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 11 MB content (exceeds default 10 MB limit)
        var content = new byte[11 * 1024 * 1024];
        Array.Fill(content, (byte)'x');

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new ByteArrayContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(2); // Not cached due to size
    }

    [Fact]
    public async Task Custom_max_size_can_be_configured()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 500 KB content
        var content = new byte[500 * 1024];
        Array.Fill(content, (byte)'x');

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new ByteArrayContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 1024 * 1024 // 1 MB
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Cached within custom limit
    }

    [Fact]
    public async Task Response_without_ContentLength_header_is_checked_after_reading()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // 20 KB content without ContentLength header
        var content = new string('x', 20 * 1024);
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content),
            Headers = { { "Cache-Control", "max-age=3600" } }
        };

        // Remove ContentLength to simulate chunked transfer
        response.Content.Headers.ContentLength = null;

        var mockHandler = new MockHttpMessageHandler(response);

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 10 * 1024 // 10 KB
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request should work but not cache (size check after reading)
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second request should hit origin (not cached)
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Zero_size_limit_prevents_all_caching()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("small"),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 0
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(2); // No caching with zero limit
    }

    [Fact]
    public async Task Empty_response_is_cacheable()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(""),
            Headers = { { "Cache-Control", "max-age=3600" } }
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = 1024
        };

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        await client.GetAsync(TestUrl, _ct);
        await client.GetAsync(TestUrl, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Empty response cached
    }
}

