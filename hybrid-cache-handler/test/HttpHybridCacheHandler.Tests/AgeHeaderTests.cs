// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.HttpHybridCacheHandler;

public class AgeHeaderTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cached_response_emits_age_header()
    {
        var now = DateTimeOffset.Parse("2024-01-01T12:00:00Z");
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Date", now.ToString("R") }
            }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        fixture.SetUtcNow(now);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct);

        fixture.AdvanceTime(TimeSpan.FromSeconds(3));
        var cachedResponse = await client.GetAsync("https://example.com/resource", _ct);

        var ageSeconds = ParseAgeSeconds(cachedResponse);
        ageSeconds.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Cached_response_updates_age_from_stored_age_value()
    {
        var now = DateTimeOffset.Parse("2024-01-01T12:00:00Z");
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Date", now.ToString("R") },
                { "Age", "30" }
            }
        });
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        fixture.SetUtcNow(now);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct);

        fixture.AdvanceTime(TimeSpan.FromSeconds(3));
        var cachedResponse = await client.GetAsync("https://example.com/resource", _ct);

        var ageSeconds = ParseAgeSeconds(cachedResponse);
        ageSeconds.ShouldBeGreaterThanOrEqualTo(33);
    }

    private static int ParseAgeSeconds(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Age", out var values).ShouldBeTrue();
        int.TryParse(values!.FirstOrDefault(), out var ageSeconds).ShouldBeTrue();
        return ageSeconds;
    }
}
