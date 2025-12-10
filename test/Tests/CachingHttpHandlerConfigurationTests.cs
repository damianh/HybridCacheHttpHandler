// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace DamianH.HybridCacheHttpHandler;

public class CachingHttpHandlerConfigurationTests
{
    private const string TestUrl = "https://example.com/resource";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Configure_default_cache_duration()
    {
        var timeProvider = new FakeTimeProvider();
        var cache = TestHelpers.CreateCache();
        var defaultDuration = TimeSpan.FromMinutes(10);

        var mockHandler = new MockHttpMessageHandler(async _ =>
        {
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("response"),
                Headers = { { "Cache-Control", "public" } }
            };
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            DefaultCacheDuration = defaultDuration
        };
        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // First request - cache miss
        await client.GetAsync(TestUrl, _ct);
        mockHandler.RequestCount.ShouldBe(1);

        // Advance time but stay within default duration
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await client.GetAsync(TestUrl, _ct);
        mockHandler.RequestCount.ShouldBe(1);

        // Advance past default duration
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        await client.GetAsync(TestUrl, _ct);
        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Respect_max_entry_size_limit()
    {
        var timeProvider = new FakeTimeProvider();
        var cache = TestHelpers.CreateCache();
        var maxSize = 100L;

        var mockHandler = new MockHttpMessageHandler(async request =>
        {
            await Task.Yield();
            var content = request.RequestUri!.PathAndQuery.Contains("large")
                ? new string('x', 200)
                : "small";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content),
                Headers = { { "Cache-Control", "public, max-age=3600" } }
            };
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = maxSize
        };
        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // Small response - should be cached
        await client.GetAsync($"{TestUrl}/small", _ct);
        mockHandler.RequestCount.ShouldBe(1);

        await client.GetAsync($"{TestUrl}/small", _ct);
        mockHandler.RequestCount.ShouldBe(1);

        // Large response - should NOT be cached
        await client.GetAsync($"{TestUrl}/large", _ct);
        mockHandler.RequestCount.ShouldBe(2);

        await client.GetAsync($"{TestUrl}/large", _ct);
        mockHandler.RequestCount.ShouldBe(3);
    }

    [Fact]
    public async Task Allow_unlimited_entry_size_when_configured()
    {
        var timeProvider = new FakeTimeProvider();
        var cache = TestHelpers.CreateCache();

        var mockHandler = new MockHttpMessageHandler(async _ =>
        {
            await Task.Yield();
            var largeContent = new string('x', 10_000_000); // 10MB

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(largeContent),
                Headers = { { "Cache-Control", "public, max-age=3600" } }
            };
        });

        var options = new HybridCacheHttpHandlerOptions
        {
            MaxCacheableContentSize = null // Unlimited
        };
        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // Large response - should be cached when unlimited
        await client.GetAsync(TestUrl, _ct);
        mockHandler.RequestCount.ShouldBe(1);

        await client.GetAsync(TestUrl, _ct);
        mockHandler.RequestCount.ShouldBe(1);
    }
}
