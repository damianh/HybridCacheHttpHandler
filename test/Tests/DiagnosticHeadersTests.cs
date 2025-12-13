// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class DiagnosticHeadersTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string TestUrl = "https://example.com/resource";

    [Fact]
    public async Task Diagnostic_headers_included_when_enabled()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test content")
            };
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromMinutes(10)
            };
            return Task.FromResult(response);
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = true
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        // First request - should be a miss
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response1.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("MISS");

        // Second request - should be a hit
        var response2 = await client.GetAsync(TestUrl, _ct);
        response2.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response2.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("HIT-FRESH");
        response2.Headers.Contains("X-Cache-Age").ShouldBeTrue();
        response2.Headers.Contains("X-Cache-MaxAge").ShouldBeTrue();
    }

    [Fact]
    public async Task Diagnostic_headers_not_included_when_disabled()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test content")
            };
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromMinutes(10)
            };
            return Task.FromResult(response);
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = false // Explicitly disabled
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync(TestUrl, _ct);
        response.Headers.Contains("X-Cache-Diagnostic").ShouldBeFalse();
        response.Headers.Contains("X-Cache-Age").ShouldBeFalse();
        response.Headers.Contains("X-Cache-MaxAge").ShouldBeFalse();
    }

    [Fact]
    public async Task Diagnostic_headers_show_bypass_for_non_cacheable_methods()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test content")
            });
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = true
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        var response = await client.PostAsync(TestUrl, new StringContent("data"), _ct);
        response.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("BYPASS-METHOD");
    }

    [Fact]
    public async Task Diagnostic_headers_show_bypass_for_no_store()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test content")
            };
            return Task.FromResult(response);
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = true
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, TestUrl);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoStore = true
        };

        var response = await client.SendAsync(request, _ct);
        response.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("BYPASS-NO-STORE");
    }

    [Fact]
    public async Task Diagnostic_headers_show_stale_while_revalidate()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test content")
            };
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(5)
            };
            response.Headers.Add("Cache-Control", "stale-while-revalidate=30");
            return Task.FromResult(response);
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = true
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        // First request - cache it
        await client.GetAsync(TestUrl, _ct);

        // Advance time to make it stale but within stale-while-revalidate window
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Second request - should serve stale
        var response = await client.GetAsync(TestUrl, _ct);
        response.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("HIT-STALE-WHILE-REVALIDATE");
    }

    [Fact]
    public async Task Diagnostic_headers_show_compression()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var content = new string('x', 2000); // Large enough to trigger compression
        var mockHandler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromMinutes(10)
            };
            return Task.FromResult(response);
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            IncludeDiagnosticHeaders = true,
            CompressionThreshold = 1024
        };

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        using var client = new HttpClient(handler);

        // First request - cache it
        await client.GetAsync(TestUrl, _ct);

        // Second request - should be compressed
        var response = await client.GetAsync(TestUrl, _ct);
        response.Headers.Contains("X-Cache-Diagnostic").ShouldBeTrue();
        response.Headers.GetValues("X-Cache-Diagnostic").First().ShouldBe("HIT-FRESH");
        response.Headers.Contains("X-Cache-Compressed").ShouldBeTrue();
        response.Headers.GetValues("X-Cache-Compressed").First().ShouldBe("true");
    }
}
