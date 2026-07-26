// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

namespace DamianH.HttpHybridCacheHandler;

public class InvalidationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unsafe_method_with_success_response_invalidates_target_uri()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(CreateCacheableResponse("value"));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct); // MISS
        await client.GetAsync("https://example.com/resource", _ct); // HIT
        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);
        await client.GetAsync("https://example.com/resource", _ct); // MISS after invalidation

        mockHandler.RequestCount.ShouldBe(3);
    }

    [Fact]
    public async Task Unsafe_method_invalidates_all_vary_variants_for_uri()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            var response = CreateCacheableResponse("value");
            response.Headers.Vary.Add("Accept");
            return Task.FromResult(response);
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(
            mockHandler,
            options => options.VaryHeaders = ["Accept"]);
        using var client = fixture.CreateClient();

        var jsonRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        jsonRequest.Headers.Add("Accept", "application/json");
        await client.SendAsync(jsonRequest, _ct); // MISS variant 1

        var xmlRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        xmlRequest.Headers.Add("Accept", "application/xml");
        await client.SendAsync(xmlRequest, _ct); // MISS variant 2

        var jsonHitRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        jsonHitRequest.Headers.Add("Accept", "application/json");
        await client.SendAsync(jsonHitRequest, _ct); // HIT

        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);

        var jsonAfterInvalidation = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        jsonAfterInvalidation.Headers.Add("Accept", "application/json");
        await client.SendAsync(jsonAfterInvalidation, _ct); // MISS variant 1

        var xmlAfterInvalidation = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        xmlAfterInvalidation.Headers.Add("Accept", "application/xml");
        await client.SendAsync(xmlAfterInvalidation, _ct); // MISS variant 2

        mockHandler.RequestCount.ShouldBe(5);
    }

    [Fact]
    public async Task Same_origin_location_uri_is_invalidated()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var response = new HttpResponseMessage(HttpStatusCode.NoContent);
                response.Headers.Location = new Uri("/other", UriKind.Relative);
                return Task.FromResult(response);
            }

            return Task.FromResult(CreateCacheableResponse(request.RequestUri!.AbsolutePath));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/other", _ct); // MISS
        await client.GetAsync("https://example.com/other", _ct); // HIT
        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);
        await client.GetAsync("https://example.com/other", _ct); // MISS after Location invalidation

        mockHandler.RequestCount.ShouldBe(3);
    }

    [Fact]
    public async Task Same_origin_content_location_uri_is_invalidated()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var response = new HttpResponseMessage(HttpStatusCode.NoContent)
                {
                    Content = new StringContent("done")
                };
                response.Content.Headers.ContentLocation = new Uri("/other", UriKind.Relative);
                return Task.FromResult(response);
            }

            return Task.FromResult(CreateCacheableResponse(request.RequestUri!.AbsolutePath));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/other", _ct); // MISS
        await client.GetAsync("https://example.com/other", _ct); // HIT
        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);
        await client.GetAsync("https://example.com/other", _ct); // MISS after Content-Location invalidation

        mockHandler.RequestCount.ShouldBe(3);
    }

    [Fact]
    public async Task Cross_origin_location_is_not_invalidated()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var response = new HttpResponseMessage(HttpStatusCode.NoContent);
                response.Headers.Location = new Uri("https://other.example/other", UriKind.Absolute);
                return Task.FromResult(response);
            }

            return Task.FromResult(CreateCacheableResponse(request.RequestUri!.AbsolutePath));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/other", _ct); // MISS
        await client.GetAsync("https://example.com/other", _ct); // HIT
        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);
        await client.GetAsync("https://example.com/other", _ct); // Still HIT

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Unsafe_method_with_error_response_does_not_invalidate()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(CreateCacheableResponse("value"));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct); // MISS
        await client.GetAsync("https://example.com/resource", _ct); // HIT
        await client.PostAsync("https://example.com/resource", new StringContent("x"), _ct);
        await client.GetAsync("https://example.com/resource", _ct); // Still HIT

        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Safe_method_does_not_invalidate()
    {
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Options)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(CreateCacheableResponse("value"));
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        using var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct); // MISS
        await client.GetAsync("https://example.com/resource", _ct); // HIT
        using var optionsRequest = new HttpRequestMessage(HttpMethod.Options, "https://example.com/resource");
        await client.SendAsync(optionsRequest, _ct);
        await client.GetAsync("https://example.com/resource", _ct); // Still HIT

        mockHandler.RequestCount.ShouldBe(2);
    }

    private static HttpResponseMessage CreateCacheableResponse(string content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
        response.Headers.CacheControl = new CacheControlHeaderValue
        {
            MaxAge = TimeSpan.FromHours(1)
        };
        return response;
    }
}
