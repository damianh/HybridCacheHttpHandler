// Copyright Damian Hickey

using BenchmarkDotNet.Attributes;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

/// <summary>
/// Benchmarks focusing on memory allocation patterns and LOH behavior
/// with content/metadata separation architecture.
/// </summary>
[MemoryDiagnoser]
public class MemoryAllocationBenchmarks
{
    private HttpClient _cachedClient = null!;
    private FakeHttpMessageHandler _fakeHandler = null!;
    private const string TestUrl = "https://example.com/api/data";

    [Params(1024, 10 * 1024, 50 * 1024, 100 * 1024, 500 * 1024, 1024 * 1024)]
    public int ResponseSize { get; set; }

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
            new HybridCacheHttpHandlerOptions
            {
                // Enable compression to test LOH mitigation
                CompressionThreshold = 1024,
                MaxCacheableContentSize = 2 * 1024 * 1024 // 2MB
            },
            NullLogger<HybridCacheHttpHandler>.Instance);

        _cachedClient = new HttpClient(cacheHandler);
    }

    [Benchmark(Description = "Cache Miss - Initial Store")]
    public async Task<string> CacheMiss_InitialStore()
    {
        // Clear cache state between iterations
        _fakeHandler.ResetCounter();
        
        var response = await _cachedClient.GetAsync($"{TestUrl}?size={ResponseSize}&key={Guid.NewGuid()}");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Cache Hit - Retrieve from Cache")]
    public async Task<string> CacheHit_RetrieveFromCache()
    {
        // Prime cache once
        var key = $"cache-hit-{ResponseSize}";
        await _cachedClient.GetAsync($"{TestUrl}?size={ResponseSize}&key={key}");
        
        // Measure retrieval
        var response = await _cachedClient.GetAsync($"{TestUrl}?size={ResponseSize}&key={key}");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Cache Hit - Multiple Retrievals")]
    public async Task MultipleRetrievals_SameContent()
    {
        // Prime cache once
        var key = $"multi-hit-{ResponseSize}";
        await _cachedClient.GetAsync($"{TestUrl}?size={ResponseSize}&key={key}");
        
        // Measure multiple retrievals (tests content deduplication)
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _cachedClient.GetAsync($"{TestUrl}?size={ResponseSize}&key={key}"))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public void Cleanup()
        => _cachedClient.Dispose();

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private int _requestCount;

        public void ResetCounter() => _requestCount = 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            // Parse size from query string
            var query = request.RequestUri?.Query ?? "";
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(query, @"size=(\d+)");
            var size = sizeMatch.Success ? int.Parse(sizeMatch.Groups[1].Value) : 1024;

            // Generate compressible content (repeating pattern)
            var content = new string('x', size);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content),
                Headers = { { "Cache-Control", "max-age=3600" } }
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

            return Task.FromResult(response);
        }
    }
}
