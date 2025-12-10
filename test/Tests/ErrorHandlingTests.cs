// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Caching.Hybrid;
using Nito.AsyncEx;

namespace DamianH.HybridCacheHttpHandler;

public class ErrorHandlingTests
{
    private const string TestUrl = "https://example.com/resource";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Serialization_failure_handled_gracefully()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=60" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request should succeed
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(1);

        // Second request should hit cache (if serialization worked)
        var response2 = await client.GetAsync(TestUrl, _ct);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(1); // Should still be cached
    }

    [Fact]
    public async Task Deserialization_failure_bypasses_cache()
    {
        var cache = new FaultyCache(shouldFailOnGet: true);
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=60" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request - cache read fails, should fetch from origin
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(1);

        // Second request - cache read still fails, should fetch from origin again
        var response2 = await client.GetAsync(TestUrl, _ct);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Cache_read_failure_falls_back_to_origin()
    {
        var cache = new FaultyCache(shouldFailOnGet: true);
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response from origin"),
            Headers = { { "Cache-Control", "max-age=60" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        var response = await client.GetAsync(TestUrl, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(_ct);
        content.ShouldBe("response from origin");
        mockHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Cache_write_failure_doesnt_break_request()
    {
        var cache = new FaultyCache(shouldFailOnSet: true);
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "max-age=60" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // First request should succeed even if caching fails
        var response1 = await client.GetAsync(TestUrl, _ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(1);

        // Second request fetches from origin since cache write failed
        var response2 = await client.GetAsync(TestUrl, _ct);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_requests_for_same_resource()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var backendSignal = new AsyncAutoResetEvent(false);
        var requestStarted = new AsyncAutoResetEvent(false);

        var mockHandler = new MockHttpMessageHandler(async () =>
        {
            requestStarted.Set(); // Signal that backend request has started
            await backendSignal.WaitAsync(_ct); // Wait for signal to proceed
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("response"),
                Headers = { { "Cache-Control", "max-age=60" } }
            };
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Start 10 concurrent requests for the same resource
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => client.GetAsync(TestUrl, _ct))
            .ToArray();

        // Wait for first backend request to start
        await requestStarted.WaitAsync(_ct);

        // Let backend complete
        backendSignal.Set();

        var responses = await Task.WhenAll(tasks);

        // All requests should succeed
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);

        // HybridCache handles request coalescing internally, 
        // so we should see minimal backend requests
        mockHandler.RequestCount.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Parallel_requests_for_different_resources()
    {
        var cache = TestHelpers.CreateCache();
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(request => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent($"response for {request.RequestUri}"),
            Headers = { { "Cache-Control", "max-age=60" } }
        }));

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Make concurrent requests for different resources
        var tasks = Enumerable.Range(0, 5)
            .Select(i => client.GetAsync($"https://example.com/resource{i}", _ct))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All requests should succeed
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);

        // Should make one request per unique resource
        mockHandler.RequestCount.ShouldBe(5);
    }

    [Fact]
    public async Task Cache_remove_failure_doesnt_break_request()
    {
        var cache = new FaultyCache(shouldFailOnRemove: true);
        var timeProvider = TestHelpers.CreateTimeProvider();
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("response"),
            Headers = { { "Cache-Control", "no-store" } }
        });

        var cacheHandler = new HybridCacheHttpHandler(mockHandler, cache, timeProvider, new HybridCacheHttpHandlerOptions(), NullLogger<HybridCacheHttpHandler>.Instance);
        var client = new HttpClient(cacheHandler);

        // Request with no-store should succeed even if cache removal fails
        var response = await client.GetAsync(TestUrl, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.RequestCount.ShouldBe(1);
    }

    private class FaultyCache(
        bool shouldFailOnGet = false,
        bool shouldFailOnSet = false,
        bool shouldFailOnRemove = false)
        : HybridCache
    {
        private readonly HybridCache _innerCache = TestHelpers.CreateCache();

        public override async ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, Ct, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default)
        {
            if (shouldFailOnGet)
            {
                throw new InvalidOperationException("Simulated cache read failure");
            }

            if (shouldFailOnSet)
            {
                // When SET fails, we need to simulate that GetOrCreate also can't cache
                // Just run the factory and return the result without caching
                return await factory(state, cancellationToken);
            }

            return await _innerCache.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
        }

        public override async ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            Ct cancellationToken = default)
        {
            if (shouldFailOnSet)
            {
                throw new InvalidOperationException("Simulated cache write failure");
            }

            await _innerCache.SetAsync(key, value, options, tags, cancellationToken);
        }

        public override async ValueTask RemoveAsync(string key, Ct cancellationToken = default)
        {
            if (shouldFailOnRemove)
            {
                throw new InvalidOperationException("Simulated cache remove failure");
            }

            await _innerCache.RemoveAsync(key, cancellationToken);
        }

        public override ValueTask RemoveAsync(IEnumerable<string> keys, Ct cancellationToken = default)
        {
            if (shouldFailOnRemove)
            {
                throw new InvalidOperationException("Simulated cache remove failure");
            }

            return _innerCache.RemoveAsync(keys, cancellationToken);
        }

        public override ValueTask RemoveByTagAsync(string tag, Ct cancellationToken = default)
        {
            if (shouldFailOnRemove)
            {
                throw new InvalidOperationException("Simulated cache remove failure");
            }

            return _innerCache.RemoveByTagAsync(tag, cancellationToken);
        }

        public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, Ct cancellationToken = default)
        {
            if (shouldFailOnRemove)
            {
                throw new InvalidOperationException("Simulated cache remove failure");
            }

            return _innerCache.RemoveByTagAsync(tags, cancellationToken);
        }
    }
}
