// Copyright Damian Hickey

using BenchmarkDotNet.Attributes;
using DamianH.HybridCacheHttpHandler;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

/// <summary>
/// Benchmarks specifically targeting Large Object Heap (LOH) behavior.
/// Tests responses around and above the 85KB LOH threshold.
/// </summary>
[MemoryDiagnoser]
public class LohBenchmarks2
{
    private HttpClient _cachedClientWithCompression = null!;
    private HttpClient _cachedClientWithoutCompression = null!;
    private FakeHttpMessageHandler _fakeHandler = null!;
    private const string TestUrl = "https://example.com/api/data";

    [GlobalSetup]
    public void Setup()
    {
        _fakeHandler = new FakeHttpMessageHandler();

        var services = new ServiceCollection();
        services.AddHybridCache();
        var serviceProvider = services.BuildServiceProvider();

        // Client with compression
        var cacheHandlerWithCompression = new HybridCacheHttpHandler(
            _fakeHandler,
            serviceProvider.GetRequiredService<HybridCache>(),
            TimeProvider.System,
            new HybridCacheHttpHandlerOptions
            {
                CompressionThreshold = 1024, // Compress >1KB
                MaxCacheableContentSize = 10 * 1024 * 1024
            },
            NullLogger<HybridCacheHttpHandler>.Instance);

        _cachedClientWithCompression = new HttpClient(cacheHandlerWithCompression);

        // Client without compression
        var cacheHandlerWithoutCompression = new HybridCacheHttpHandler(
            new FakeHttpMessageHandler(),
            serviceProvider.GetRequiredService<HybridCache>(),
            TimeProvider.System,
            new HybridCacheHttpHandlerOptions
            {
                CompressionThreshold = null, // Disable compression
                MaxCacheableContentSize = 10 * 1024 * 1024
            },
            NullLogger<HybridCacheHttpHandler>.Instance);

        _cachedClientWithoutCompression = new HttpClient(cacheHandlerWithoutCompression);
    }

    [Benchmark(Description = "Below LOH: 80KB - Uncompressed")]
    public async Task BelowLOH_80KB_Uncompressed()
    {
        var size = 80 * 1024; // 80KB - just below LOH threshold
        await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=below-loh-uncomp");
        var response = await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=below-loh-uncomp");
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "At LOH Threshold: 85KB - Uncompressed")]
    public async Task AtLOH_85KB_Uncompressed()
    {
        var size = 85 * 1024; // 85KB - at LOH threshold
        await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=at-loh-uncomp");
        var response = await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=at-loh-uncomp");
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Above LOH: 100KB - Uncompressed")]
    public async Task AboveLOH_100KB_Uncompressed()
    {
        var size = 100 * 1024; // 100KB - above LOH threshold
        await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=above-loh-uncomp");
        var response = await _cachedClientWithoutCompression.GetAsync($"{TestUrl}?size={size}&key=above-loh-uncomp");
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Above LOH: 100KB - With Compression")]
    public async Task AboveLOH_100KB_WithCompression()
    {
        var size = 100 * 1024; // 100KB - compression should reduce significantly
        await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=above-loh-comp");
        var response = await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=above-loh-comp");
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Large: 500KB - With Compression")]
    public async Task Large_500KB_WithCompression()
    {
        var size = 500 * 1024; // 500KB
        await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=large-comp");
        var response = await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=large-comp");
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark(Description = "Very Large: 1MB - With Compression")]
    public async Task VeryLarge_1MB_WithCompression()
    {
        var size = 1024 * 1024; // 1MB
        await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=very-large-comp");
        var response = await _cachedClientWithCompression.GetAsync($"{TestUrl}?size={size}&key=very-large-comp");
        await response.Content.ReadAsStringAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cachedClientWithCompression.Dispose();
        _cachedClientWithoutCompression.Dispose();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri?.Query ?? "";
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(query, @"size=(\d+)");
            var size = sizeMatch.Success ? int.Parse(sizeMatch.Groups[1].Value) : 1024;

            // Generate highly compressible content (repeating pattern)
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
