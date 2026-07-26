// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

namespace DamianH.HttpHybridCacheHandler;

public class OptionalConformanceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connection_listed_hop_by_hop_headers_are_not_replayed_from_cache()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("cached")
        };
        response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };
        response.Headers.TryAddWithoutValidation("Connection", "X-Test-Hop");
        response.Headers.TryAddWithoutValidation("X-Test-Hop", "secret");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/connection", _ct);
        var cachedResponse = await client.GetAsync("https://example.com/connection", _ct);

        mockHandler.RequestCount.ShouldBe(1);
        cachedResponse.Headers.Contains("Connection").ShouldBeFalse();
        cachedResponse.Headers.Contains("X-Test-Hop").ShouldBeFalse();
    }

    [Fact]
    public async Task Qualified_no_cache_headers_are_omitted_without_forcing_revalidation()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("cached")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "max-age=3600, no-cache=\"Set-Cookie\"");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "session=abc");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/qualified-no-cache", _ct);
        var cachedResponse = await client.GetAsync("https://example.com/qualified-no-cache", _ct);

        mockHandler.RequestCount.ShouldBe(1);
        cachedResponse.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Shared_cache_allows_authorization_with_must_revalidate()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("authorized")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "must-revalidate, max-age=60");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            options => options.Mode = CacheMode.Shared);
        using var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/auth");
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/auth");
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Shared_cache_reuses_authorized_must_revalidate_response_for_followup_without_authorization()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("authorized")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "must-revalidate, max-age=3600");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            options => options.Mode = CacheMode.Shared);
        using var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/auth-followup");
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        request1.Headers.TryAddWithoutValidation("Pragma", "foo");
        request1.Headers.TryAddWithoutValidation("Cache-Control", "nothing-to-see-here");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/auth-followup");
        request2.Headers.TryAddWithoutValidation("Pragma", "foo");
        request2.Headers.TryAddWithoutValidation("Cache-Control", "nothing-to-see-here");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Must_understand_can_override_no_store_for_understood_status()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("cacheable")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "no-store, must-understand, max-age=120");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/must-understand", _ct);
        await client.GetAsync("https://example.com/must-understand", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Cdn_cache_control_can_override_cache_control_no_store()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("cdn")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "no-store");
        response.Headers.TryAddWithoutValidation("CDN-Cache-Control", "max-age=60");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            options => options.Mode = CacheMode.Shared);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/cdn", _ct);
        await client.GetAsync("https://example.com/cdn", _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Shared_cache_recognizes_cdn_cache_control_max_age()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("cdn")
        };
        response.Headers.TryAddWithoutValidation("CDN-Cache-Control", "max-age=3600");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            options => options.Mode = CacheMode.Shared);
        using var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/cdn-max-age");
        request1.Headers.TryAddWithoutValidation("Pragma", "foo");
        request1.Headers.TryAddWithoutValidation("Cache-Control", "nothing-to-see-here");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/cdn-max-age");
        request2.Headers.TryAddWithoutValidation("Pragma", "foo");
        request2.Headers.TryAddWithoutValidation("Cache-Control", "nothing-to-see-here");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Head_response_can_refresh_cached_get_freshness()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            requestCount++;
            if (req.Method == HttpMethod.Head)
            {
                var head = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty)
                };
                head.Headers.TryAddWithoutValidation("Cache-Control", "max-age=120");
                head.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return Task.FromResult(head);
            }

            var get = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("v1")
            };
            get.Headers.TryAddWithoutValidation("Cache-Control", "max-age=1");
            get.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
            return Task.FromResult(get);
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/head-refresh", _ct);
        fixture.AdvanceTime(TimeSpan.FromSeconds(2));
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://example.com/head-refresh"), _ct);

        var response = await client.GetAsync("https://example.com/head-refresh", _ct);
        var body = await response.Content.ReadAsStringAsync(_ct);

        requestCount.ShouldBe(2); // Initial GET + HEAD only (final GET served from cache)
        body.ShouldBe("v1");
    }

    [Fact]
    public async Task Head_error_can_invalidate_cached_get()
    {
        var getResponses = 0;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Head)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Gone)
                {
                    Content = new StringContent(string.Empty)
                });
            }

            getResponses++;
            var get = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"v{getResponses}")
            };
            get.Headers.TryAddWithoutValidation("Cache-Control", "max-age=3600");
            get.Headers.ETag = new EntityTagHeaderValue($"\"v{getResponses}\"");
            return Task.FromResult(get);
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/head-invalidate", _ct);
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://example.com/head-invalidate"), _ct);
        var response = await client.GetAsync("https://example.com/head-invalidate", _ct);
        var body = await response.Content.ReadAsStringAsync(_ct);

        mockHandler.RequestCount.ShouldBe(3);
        body.ShouldBe("v2");
    }

    [Fact]
    public async Task Stale_response_can_be_served_on_5xx_without_stale_if_error()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var initial = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("cached")
                };
                initial.Headers.TryAddWithoutValidation("Cache-Control", "max-age=1");
                initial.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return initial;
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("origin-down")
            };
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/stale-5xx", _ct);
        fixture.AdvanceTime(TimeSpan.FromSeconds(2));

        var response = await client.GetAsync("https://example.com/stale-5xx", _ct);
        var body = await response.Content.ReadAsStringAsync(_ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldBe("cached");
    }

    [Fact]
    public async Task Stale_response_can_be_served_on_transport_error()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var initial = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("cached")
                };
                initial.Headers.TryAddWithoutValidation("Cache-Control", "max-age=1");
                initial.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return Task.FromResult(initial);
            }

            throw new HttpRequestException("connection closed");
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/stale-close", _ct);
        fixture.AdvanceTime(TimeSpan.FromSeconds(2));

        var response = await client.GetAsync("https://example.com/stale-close", _ct);
        var body = await response.Content.ReadAsStringAsync(_ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldBe("cached");
    }

    [Fact]
    public async Task Fresh_complete_response_can_satisfy_single_range_request()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("abcdef")
        };
        response.Headers.TryAddWithoutValidation("Cache-Control", "max-age=3600");

        var mockHandler = new MockHttpMessageHandler(response);
        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/range-full", _ct);

        var rangeRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com/range-full");
        rangeRequest.Headers.Range = new RangeHeaderValue(1, 3);
        var rangeResponse = await client.SendAsync(rangeRequest, _ct);
        var body = await rangeResponse.Content.ReadAsStringAsync(_ct);

        mockHandler.RequestCount.ShouldBe(1);
        rangeResponse.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        body.ShouldBe("bcd");
        rangeResponse.Content.Headers.ContentRange.ShouldNotBeNull();
        rangeResponse.Content.Headers.ContentRange.From.ShouldBe(1);
        rangeResponse.Content.Headers.ContentRange.To.ShouldBe(3);
    }

    [Fact]
    public async Task Partial_response_is_cached_per_range_request()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StringContent("ab")
            };
            response.Headers.TryAddWithoutValidation("Cache-Control", "max-age=3600");
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 1, 6);
            response.Headers.AcceptRanges.Add("bytes");
            return Task.FromResult(response);
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/range-partial");
        request1.Headers.Range = new RangeHeaderValue(0, 1);
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/range-partial");
        request2.Headers.Range = new RangeHeaderValue(0, 1);
        var response2 = await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
        response2.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
    }
}
