// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HybridCacheHttpHandler;

public class MinFreshTests
{
    private const string TestUrl = "https://example.com/resource";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task MinFresh_RejectsResponseThatWillExpireSoon()
    {
        // Arrange
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("test content"),
            Headers = { { "Cache-Control", "max-age=30" } }
        });

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // First request - populate cache
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Advance time by 10 seconds (leaving 20 seconds freshness)
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Second request with min-fresh=30 (requires 30 seconds of remaining freshness)
        var request2 = new HttpRequestMessage(HttpMethod.Get, TestUrl);
        request2.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            MinFresh = TimeSpan.FromSeconds(30) // Requires response to be fresh for at least 30 more seconds
        };

        var response2 = await client.SendAsync(request2, _ct);

        // Should get fresh response from origin since cached has only 20 seconds left
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response2.Content.ReadAsStringAsync(_ct)).ShouldBe("test content");
        mockHandler.RequestCount.ShouldBe(2); // Cache miss, went to origin again
    }

    [Fact]
    public async Task MinFresh_AcceptsResponseWithSufficientFreshness()
    {
        // Arrange
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("content-1"),
            Headers = { { "Cache-Control", "max-age=100" } }
        });

        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // First request - populate cache
        var response1 = await client.GetAsync(TestUrl, _ct);
        (await response1.Content.ReadAsStringAsync(_ct)).ShouldBe("content-1");

        // Advance time by 10 seconds (leaving 90 seconds freshness)
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Second request with min-fresh=30 (requires 30 seconds of remaining freshness)
        var request2 = new HttpRequestMessage(HttpMethod.Get, TestUrl);
        request2.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            MinFresh = TimeSpan.FromSeconds(30)
        };

        var response2 = await client.SendAsync(request2, _ct);

        // Should get cached response since it has 90 seconds left (> 30 required)
        (await response2.Content.ReadAsStringAsync(_ct)).ShouldBe("content-1");
        mockHandler.RequestCount.ShouldBe(1); // No additional backend call
    }

    [Fact]
    public async Task MinFresh_WorksWithExpiresHeader()
    {
        // Arrange
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        timeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test content")
        };
        response.Content.Headers.Expires = new DateTimeOffset(2024, 1, 1, 12, 1, 0, TimeSpan.Zero);

        var mockHandler = new MockHttpMessageHandler(response);
        var handler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(handler);

        // First request
        await client.GetAsync(TestUrl, _ct);

        // Advance by 20 seconds (40 seconds of freshness remaining)
        timeProvider.Advance(TimeSpan.FromSeconds(20));

        // Request with min-fresh=50 should bypass cache
        var request2 = new HttpRequestMessage(HttpMethod.Get, TestUrl);
        request2.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            MinFresh = TimeSpan.FromSeconds(50) // Needs 50 seconds, only 40 available
        };

        var response2 = await client.SendAsync(request2, _ct);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(2); // Should have bypassed cache
    }
}
