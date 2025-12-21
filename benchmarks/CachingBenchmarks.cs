// Copyright Damian Hickey

using BenchmarkDotNet.Attributes;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

[MemoryDiagnoser]
public class CachingBenchmarks
{
    private HttpClient _cachedClient = null!;
    private HttpClient _uncachedClient = null!;
    private FakeHttpMessageHandler _fakeHandler = null!;
    private const string TestUrl = "https://example.com/api/data";

    [GlobalSetup]
    public void Setup()
    {
        _fakeHandler = new FakeHttpMessageHandler();

        var services = new ServiceCollection();
        services.AddHybridCache();
        var serviceProvider = services.BuildServiceProvider();

        var cacheHandler = new HybridCacheHttpHandler(
            _fakeHandler,
            serviceProvider.GetRequiredService<HybridCache>(),
            TimeProvider.System,
            new HybridCacheHttpHandlerOptions(),
            NullLogger<HybridCacheHttpHandler>.Instance);

        _cachedClient = new HttpClient(cacheHandler);
        _uncachedClient = new HttpClient(_fakeHandler);
    }

    [Benchmark(Baseline = true)]
    public async Task<string> UncachedRequest()
    {
        var response = await _uncachedClient.GetAsync(TestUrl);
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task<string> CachedRequest_FirstHit()
    {
        _fakeHandler.ResetCounter();
        var response = await _cachedClient.GetAsync(TestUrl);
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task<string> CachedRequest_CacheHit()
    {
        // Prime cache
        await _cachedClient.GetAsync(TestUrl);
        _fakeHandler.ResetCounter();

        // Measure cached hit
        var response = await _cachedClient.GetAsync(TestUrl);
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task ConcurrentRequests_10Parallel()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _cachedClient.GetAsync(TestUrl + "?concurrent=true"))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task<string> LargeResponse_10KB()
    {
        var response = await _cachedClient.GetAsync(TestUrl + "?size=large");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task<string> VaryHeader_KeyGeneration()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, TestUrl);
        request.Headers.Add("Accept-Language", "en-US");
        request.Headers.Add("Accept-Encoding", "gzip, deflate");
        var response = await _cachedClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cachedClient.Dispose();
        _uncachedClient.Dispose();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private int _requestCount;

        public void ResetCounter() => _requestCount = 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            var content = request.RequestUri?.Query.Contains("size=large") == true
                ? new string('x', 10 * 1024) // 10KB
                : $"Response #{_requestCount}";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content),
                Headers = { { "Cache-Control", "max-age=3600" } }
            };

            if (request.Headers.Contains("Accept-Language"))
            {
                response.Headers.Add("Vary", "Accept-Language, Accept-Encoding");
            }

            return Task.FromResult(response);
        }
    }
}
