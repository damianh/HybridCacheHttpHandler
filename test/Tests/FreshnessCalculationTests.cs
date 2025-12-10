// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class FreshnessCalculationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Response_fresh_until_Expires_date()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var expiresTime = timeProvider.GetUtcNow().AddHours(1);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
            {
                Headers = { Expires = expiresTime }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time to just before expiry
        timeProvider.Advance(TimeSpan.FromMinutes(59));

        // Second request - should be cached
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Expires_overridden_by_Cache_Control_max_age()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var expiresTime = timeProvider.GetUtcNow().AddHours(2); // 2 hours
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
            {
                Headers = { Expires = expiresTime }
            },
            Headers = { { "Cache-Control", "max-age=3600" } } // 1 hour - should take precedence
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time to 1.5 hours (past max-age, before Expires)
        timeProvider.Advance(TimeSpan.FromMinutes(90));

        // Second request - should fetch fresh (max-age expired, not Expires)
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Age_header_increases_response_age()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" }, // 1 hour
                { "Age", "1800" } // Already 30 minutes old
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 35 minutes (total age = 65 minutes > 60 minute max-age)
        timeProvider.Advance(TimeSpan.FromMinutes(35));

        // Second request - should fetch fresh
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Date_header_used_to_calculate_age()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var responseDate = timeProvider.GetUtcNow().AddMinutes(-30); // Response created 30 minutes ago
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" }, // 1 hour
                { "Date", responseDate.ToString("R") }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 35 minutes (total age = 65 minutes > 60 minute max-age)
        timeProvider.Advance(TimeSpan.FromMinutes(35));

        // Second request - should fetch fresh
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Current_age_correctly_reduces_freshness()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=3600" } } // 1 hour
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 30 minutes - still fresh
        timeProvider.Advance(TimeSpan.FromMinutes(30));
        await client.GetAsync("https://example.com/resource", _ct);
        mockHandler.RequestCount.ShouldBe(1);

        // Advance another 35 minutes (total 65 minutes > 60 minute max-age)
        timeProvider.Advance(TimeSpan.FromMinutes(35));
        await client.GetAsync("https://example.com/resource", _ct);
        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Last_Modified_based_heuristic_10_percent_rule()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // Resource last modified 10 days ago
        var lastModified = timeProvider.GetUtcNow().AddDays(-10);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
            {
                Headers = { LastModified = lastModified }
            }
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 12 hours (< 10% of 10 days = 24 hours)
        timeProvider.Advance(TimeSpan.FromHours(12));

        // Second request - should be cached (heuristic freshness)
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Heuristic_only_when_no_explicit_freshness_info()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var lastModified = timeProvider.GetUtcNow().AddDays(-10);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
            {
                Headers = { LastModified = lastModified }
            },
            Headers = { { "Cache-Control", "max-age=600" } } // 10 minutes explicit
        });
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 12 minutes (past explicit max-age)
        timeProvider.Advance(TimeSpan.FromMinutes(12));

        // Second request - should fetch fresh (explicit max-age takes precedence)
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Configurable_heuristic_percentage()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        // Resource last modified 10 days ago
        var lastModified = timeProvider.GetUtcNow().AddDays(-10);
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response")
            {
                Headers = { LastModified = lastModified }
            }
        });

        // Configure with 20% heuristic instead of default 10%
        var options = new HybridCacheHttpHandlerOptions
        {
            HeuristicFreshnessPercent = 0.2 // 20%
        };
        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, options, NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Advance time by 30 hours (< 20% of 10 days = 48 hours)
        timeProvider.Advance(TimeSpan.FromHours(30));

        // Second request - should be cached (20% heuristic)
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }
}
