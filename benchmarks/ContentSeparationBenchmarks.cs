// Copyright Damian Hickey

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

/// <summary>
/// Benchmarks comparing the new content/metadata separation architecture
/// focusing on the overhead of two cache lookups vs memory efficiency.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class ContentSeparationBenchmarks
{
    private HttpClient _cachedClient = null!;
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
            new HybridCacheHttpHandlerOptions
            {
                CompressionThreshold = 1024,
                MaxCacheableContentSize = 10 * 1024 * 1024
            },
            NullLogger<HybridCacheHttpHandler>.Instance);

        _cachedClient = new HttpClient(cacheHandler);
    }

    [Benchmark(Description = "Small Response (1KB) - Two Lookups")]
    public async Task<string> SmallResponse_1KB()
    {
        // Prime cache
        await _cachedClient.GetAsync($"{TestUrl}?size=1024&key=small");
        
        // Measure retrieval (metadata + content lookup)
        var response = await _cachedClient.GetAsync($"{TestUrl}?size=1024&key=small");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Medium Response (50KB) - Two Lookups")]
    public async Task<string> MediumResponse_50KB()
    {
        await _cachedClient.GetAsync($"{TestUrl}?size=51200&key=medium");
        
        var response = await _cachedClient.GetAsync($"{TestUrl}?size=51200&key=medium");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Large Response (100KB) - Two Lookups")]
    public async Task<string> LargeResponse_100KB()
    {
        await _cachedClient.GetAsync($"{TestUrl}?size=102400&key=large");
        
        var response = await _cachedClient.GetAsync($"{TestUrl}?size=102400&key=large");
        return await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Content Deduplication - Same Content, Different Metadata")]
    public async Task ContentDeduplication_DifferentVaryHeaders()
    {
        var size = 50 * 1024; // 50KB

        // Request 1: en-US
        var request1 = new HttpRequestMessage(HttpMethod.Get, $"{TestUrl}?size={size}&key=dedup");
        request1.Headers.Add("Accept-Language", "en-US");
        await _cachedClient.SendAsync(request1);

        // Request 2: fr-FR (same content, different vary key)
        var request2 = new HttpRequestMessage(HttpMethod.Get, $"{TestUrl}?size={size}&key=dedup");
        request2.Headers.Add("Accept-Language", "fr-FR");
        await _cachedClient.SendAsync(request2);

        // Both should reference the same content via hash
        // Measure retrieval of second one
        var request3 = new HttpRequestMessage(HttpMethod.Get, $"{TestUrl}?size={size}&key=dedup");
        request3.Headers.Add("Accept-Language", "fr-FR");
        var response = await _cachedClient.SendAsync(request3);
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Concurrent Cache Hits - Stampede Protection")]
    public async Task ConcurrentCacheHits_10Parallel()
    {
        // Prime cache
        await _cachedClient.GetAsync($"{TestUrl}?size=10240&key=concurrent");

        // Measure concurrent retrievals (tests metadata + content lookup under concurrency)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _cachedClient.GetAsync($"{TestUrl}?size=10240&key=concurrent"))
            .ToArray();
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cachedClient.Dispose();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri?.Query ?? "";
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(query, @"size=(\d+)");
            var size = sizeMatch.Success ? int.Parse(sizeMatch.Groups[1].Value) : 1024;

            var content = new string('x', size);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content),
                Headers = { { "Cache-Control", "max-age=3600" } }
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

            // Add Vary header if Accept-Language is present
            if (request.Headers.Contains("Accept-Language"))
            {
                response.Headers.Add("Vary", "Accept-Language");
            }

            return Task.FromResult(response);
        }
    }
}
