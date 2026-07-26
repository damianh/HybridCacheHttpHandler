// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using Microsoft.Extensions.Caching.Hybrid;

namespace DamianH.HttpHybridCacheHandler;

public class VaryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vary_Accept_creates_separate_cache_entries()
    {

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Accept" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request with Accept: application/json
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request with same Accept header - should use cache
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept", "application/json");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1); // Second request uses cache

        // Third request with different Accept header - should miss cache
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept", "application/xml");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2); // Different Accept value = cache miss
    }

    [Fact]
    public async Task Vary_Accept_Encoding_handles_multiple_entries()
    {

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Accept-Encoding" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // Request with gzip
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Encoding", "gzip");
        await client.SendAsync(request1, _ct);

        // Request with br
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Encoding", "br");
        await client.SendAsync(request2, _ct);

        // Request with gzip again - should use first cache entry
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept-Encoding", "gzip");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2); // Two unique Accept-Encoding values
    }

    [Fact]
    public async Task Multiple_Vary_headers_supported()
    {

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Accept, Accept-Language" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        request1.Headers.Add("Accept-Language", "en-US");
        await client.SendAsync(request1, _ct);

        // Same headers - cache hit
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept", "application/json");
        request2.Headers.Add("Accept-Language", "en-US");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1);

        // Different Accept-Language - cache miss
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept", "application/json");
        request3.Headers.Add("Accept-Language", "fr-FR");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Vary_star_makes_response_uncacheable()
    {

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Vary", "*" } // Wildcard = uncacheable
            }
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request
        await client.GetAsync("https://example.com/resource", _ct);

        // Second request - should not use cache
        await client.GetAsync("https://example.com/resource", _ct);

        mockHandler.RequestCount.ShouldBe(2); // Vary: * prevents caching
    }

    [Fact]
    public async Task Missing_Vary_header_in_request_causes_miss()
    {

        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Accept" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request with Accept header
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request without Accept header - should miss cache
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(2); // Missing header value = cache miss
    }

    [Fact]
    public async Task Unconfigured_response_Vary_header_is_enforced()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Foo" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Foo", "1");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", "2");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Vary_header_preserves_commas_inside_values()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Foo" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Foo", "\"a, b\"");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", "\"a, b\"");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(1);

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Foo", "\"a,b\"");
        await client.SendAsync(request3, _ct);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Stored_request_missing_Vary_header_does_not_match_presented_request()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            requestCount++;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response {requestCount}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Foo" }
                }
            };
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        await client.GetAsync("https://example.com/resource", _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", "1");
        await client.SendAsync(request2, _ct);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Multiple_variants_are_stored_and_retrieved_per_response_Vary()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(async request =>
        {
            requestCount++;
            var fooValue = request.Headers.TryGetValues("Foo", out var values)
                ? values.Single()
                : "missing";

            return await Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"foo_{fooValue}"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", "Foo" }
                }
            });
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Foo", "1");
        var response1 = await client.SendAsync(request1, _ct);
        (await response1.Content.ReadAsStringAsync(_ct)).ShouldBe("foo_1");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", "2");
        var response2 = await client.SendAsync(request2, _ct);
        (await response2.Content.ReadAsStringAsync(_ct)).ShouldBe("foo_2");

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Foo", "1");
        var response3 = await client.SendAsync(request3, _ct);
        (await response3.Content.ReadAsStringAsync(_ct)).ShouldBe("foo_1");

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Duplicate_Vary_headers_do_not_outrank_more_specific_variant()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(async request =>
        {
            requestCount++;
            var hasBar = request.Headers.TryGetValues("Bar", out _);

            return await Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(hasBar ? "foo-bar" : "foo-duplicate"),
                Headers =
                {
                    { "Cache-Control", "max-age=3600" },
                    { "Vary", hasBar ? "Foo, Bar" : "Foo, Foo" }
                }
            });
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Foo", "1");
        request1.Headers.Add("Bar", "2");
        var response1 = await client.SendAsync(request1, _ct);
        (await response1.Content.ReadAsStringAsync(_ct)).ShouldBe("foo-bar");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", "1");
        var response2 = await client.SendAsync(request2, _ct);
        (await response2.Content.ReadAsStringAsync(_ct)).ShouldBe("foo-duplicate");

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Foo", "1");
        request3.Headers.Add("Bar", "2");
        var response3 = await client.SendAsync(request3, _ct);

        (await response3.Content.ReadAsStringAsync(_ct)).ShouldBe("foo-bar");
        requestCount.ShouldBe(2);
    }

    [Fact]
    public void SelectVariant_ignores_duplicate_stored_Vary_headers_when_scoring_specificity()
    {
        var olderSpecificVariant = new CachedHttpMetadata
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentKey = "specific",
            ContentLength = 0,
            Headers = new Dictionary<string, string[]>(),
            ContentHeaders = new Dictionary<string, string[]>(),
            CachedAt = DateTimeOffset.UnixEpoch,
            VaryHeaders = ["Foo", "Bar"],
            VaryHeaderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Foo"] = "1",
                ["Bar"] = "2"
            }
        };

        var newerDuplicateVariant = new CachedHttpMetadata
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentKey = "duplicate",
            ContentLength = 0,
            Headers = new Dictionary<string, string[]>(),
            ContentHeaders = new Dictionary<string, string[]>(),
            CachedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            VaryHeaders = ["Foo", "Foo"],
            VaryHeaderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Foo"] = "1"
            }
        };

        var entry = new CachedHttpEntry
        {
            Variants = [newerDuplicateVariant, olderSpecificVariant]
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Foo", "1");
        request.Headers.Add("Bar", "2");

        var selected = VaryMatcher.SelectVariant(entry, request);

        selected.ShouldNotBeNull();
        selected.ContentKey.ShouldBe("specific");
    }

    [Fact]
    public async Task Case_insensitive_Vary_header_matching()
    {

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Vary", "Accept" }
            }
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept", "application/json");
        await client.SendAsync(request1, _ct);

        // Second request with different case but same value
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("accept", "application/json"); // Different header name case
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Case-insensitive match
    }

    [Fact]
    public async Task Vary_header_values_are_normalized()
    {

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Vary", "Accept-Encoding" }
            }
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        // First request with specific order
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        await client.SendAsync(request1, _ct);

        // Second request with same values, different spacing
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Encoding", "gzip,deflate,br");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1); // Should match despite whitespace differences
    }

    [Fact]
    public async Task Unknown_Vary_header_values_are_normalized()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Vary", "Foo" }
            }
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Foo", "1,2");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Foo", " 1, 2 ");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Accept_Language_values_match_ignoring_order_and_case()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers =
            {
                { "Cache-Control", "max-age=3600" },
                { "Vary", "Accept-Language" }
            }
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Language", "en, de");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Language", "De, EN");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Accept_Language_can_match_by_Content_Language_selection()
    {
        var mockHandler = new MockHttpMessageHandler(() =>
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("response")
            };
            response.Headers.Add("Cache-Control", "max-age=3600");
            response.Headers.Add("Vary", "Accept-Language");
            response.Content.Headers.ContentLanguage.Add("de");
            return response;
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Language", "en, de");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Language", "fr;q=0.5, de;q=1.0");
        await client.SendAsync(request2, _ct);

        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public void Accept_Language_signature_uses_invariant_q_formatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var frenchCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = frenchCulture;
            CultureInfo.CurrentUICulture = frenchCulture;

            var signature = VaryMatcher.BuildVariantSignature(
                ["Accept-Language"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Accept-Language"] = "en;q=0.5"
                });

            signature.ShouldBe("accept-language=en;q=0.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task Accept_Language_prefers_variant_with_highest_quality_content_language_match()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(async request =>
        {
            requestCount++;
            var language = request.Headers.AcceptLanguage
                .FirstOrDefault()?.Value?
                .StartsWith("de", StringComparison.OrdinalIgnoreCase) == true
                ? "de"
                : "en";

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response_{language}")
            };
            response.Headers.Add("Cache-Control", "max-age=3600");
            response.Headers.Add("Vary", "Accept-Language");
            response.Content.Headers.ContentLanguage.Add(language);
            return await Task.FromResult(response);
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Language", "en");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Language", "de");
        await client.SendAsync(request2, _ct);

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept-Language", "de;q=0.9, en;q=0.8");
        var response3 = await client.SendAsync(request3, _ct);

        (await response3.Content.ReadAsStringAsync(_ct)).ShouldBe("response_de");
        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Accept_Language_quality_selection_handles_whitespace_after_q_equals()
    {
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler(async request =>
        {
            requestCount++;
            var language = request.Headers.AcceptLanguage
                .FirstOrDefault()?.Value?
                .StartsWith("de", StringComparison.OrdinalIgnoreCase) == true
                ? "de"
                : "en";

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"response_{language}")
            };
            response.Headers.Add("Cache-Control", "max-age=3600");
            response.Headers.Add("Vary", "Accept-Language");
            response.Content.Headers.ContentLanguage.Add(language);
            return await Task.FromResult(response);
        });

        var fixture = new HttpHybridCacheHandlerFixture(mockHandler);
        var client = fixture.CreateClient();

        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request1.Headers.Add("Accept-Language", "en");
        await client.SendAsync(request1, _ct);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request2.Headers.Add("Accept-Language", "de");
        await client.SendAsync(request2, _ct);

        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request3.Headers.Add("Accept-Language", "de;q= 0.1, en;q= 0.9");
        var response3 = await client.SendAsync(request3, _ct);

        (await response3.Content.ReadAsStringAsync(_ct)).ShouldBe("response_en");
        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Variant_miss_write_merges_against_latest_cached_entry()
    {
        var initialVariant = CreateStoredFooVariant("1", DateTimeOffset.UnixEpoch);
        var concurrentVariant = CreateStoredFooVariant("3", DateTimeOffset.UnixEpoch.AddSeconds(1));
        var cache = new SequencedEntryHybridCache(
            new CachedHttpEntry { Variants = [initialVariant] },
            new CachedHttpEntry { Variants = [initialVariant, concurrentVariant] });

        var mockHandler = new MockHttpMessageHandler(request =>
        {
            var foo = request.Headers.TryGetValues("Foo", out var values)
                ? string.Join(string.Empty, values)
                : "missing";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"response_{foo}")
            };
            response.Headers.Add("Cache-Control", "max-age=3600");
            response.Headers.Add("Vary", "Foo");
            return Task.FromResult(response);
        });

        await using var fixture = new HttpHybridCacheHandlerFixture(mockHandler, customCache: cache);
        using var client = fixture.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resource");
        request.Headers.Add("Foo", "2");
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        cache.LastStoredMetadataEntry.ShouldNotBeNull();

        var fooValues = cache.LastStoredMetadataEntry.Variants
            .Select(v => v.VaryHeaderValues?["Foo"])
            .Where(v => v != null)
            .Select(v => v!)
            .OrderBy(v => v)
            .ToArray();

        fooValues.ShouldBe(["1", "2", "3"]);
    }

    private static CachedHttpMetadata CreateStoredFooVariant(string fooValue, DateTimeOffset cachedAt) =>
        new()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentKey = $"content-{fooValue}",
            ContentLength = 0,
            Headers = new Dictionary<string, string[]>(),
            ContentHeaders = new Dictionary<string, string[]>(),
            CachedAt = cachedAt,
            MaxAge = TimeSpan.FromHours(1),
            VaryHeaders = ["Foo"],
            VaryHeaderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Foo"] = fooValue
            }
        };

    private sealed class SequencedEntryHybridCache(
        CachedHttpEntry initialEntry,
        CachedHttpEntry latestEntry) : HybridCache
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);
        private int _metadataReadCount;

        public CachedHttpEntry? LastStoredMetadataEntry { get; private set; }

        public override async ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, Ct, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default)
        {
            if (typeof(T) == typeof(CachedHttpEntry))
            {
                var readCount = Interlocked.Increment(ref _metadataReadCount);
                var entry = readCount == 1 ? initialEntry : latestEntry;
                return (T)(object)entry;
            }

            if (_store.TryGetValue(key, out var stored))
            {
                return (T)stored!;
            }

            var created = await factory(state, cancellationToken);
            _store[key] = created;
            return created;
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default)
        {
            if (value is CachedHttpEntry metadataEntry)
            {
                LastStoredMetadataEntry = metadataEntry;
            }
            else
            {
                _store[key] = value;
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveAsync(string key, Ct cancellationToken = default)
        {
            _store.Remove(key);
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveAsync(IEnumerable<string> keys, Ct cancellationToken = default)
        {
            foreach (var key in keys)
            {
                _store.Remove(key);
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveByTagAsync(string tag, Ct cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, Ct cancellationToken = default) => ValueTask.CompletedTask;
    }
}
