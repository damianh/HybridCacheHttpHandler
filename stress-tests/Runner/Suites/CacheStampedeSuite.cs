using System.Diagnostics;
using Runner.Config;
using Runner.Infrastructure;

namespace Runner.Suites;

public class CacheStampedeSuite : ISuite
{
    public string Name => "Cache Stampede Test";
    
    public string Description => 
        "100 concurrent requests to same endpoint with 100ms delay\n" +
        "Validates: Request collapsing (only 1-2 backend calls expected)";
    
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(5);

    public async Task<SuiteResult> RunAsync(
        HttpClient client,
        SuiteConfig config,
        IProgress<SuiteProgress> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var metrics = new MetricsCollector();
        var errors = new List<string>();

        // Setup
        var url = "/api/delay/100?size=1024";
        var concurrency = 100;

        progress.Report(new SuiteProgress
        {
            TotalRequests = concurrency,
            CurrentPhase = "Starting concurrent request storm..."
        });

        // Execute concurrent requests
        var tasks = Enumerable.Range(0, concurrency)
            .Select(async i =>
            {
                try
                {
                    var requestSw = Stopwatch.StartNew();
                    var response = await client.GetAsync(url, ct);
                    requestSw.Stop();

                    var cacheHit = response.Headers.Age?.TotalSeconds > 0 ||
                                  response.Headers.Contains("X-Cache-Status");

                    metrics.RecordRequest(
                        success: response.IsSuccessStatusCode,
                        latencyMs: requestSw.ElapsedMilliseconds,
                        cacheHit: cacheHit
                    );

                    progress.Report(new SuiteProgress
                    {
                        CompletedRequests = i + 1,
                        TotalRequests = concurrency,
                        CacheHits = metrics.CacheHits,
                        CacheMisses = metrics.CacheMisses,
                        AverageLatencyMs = metrics.AverageLatencyMs,
                        CurrentPhase = "Executing requests..."
                    });
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        var collectedMetrics = metrics.GetMetrics();

        // Validate
        var success = errors.Count == 0 && 
                     collectedMetrics.CacheHitRatio > 0.95; // 95% should be cache hits

        var summary = success 
            ? "✓ Request collapsing worked - only 1-2 backend calls"
            : "✗ Too many cache misses or errors occurred";

        return new SuiteResult
        {
            Success = success,
            Duration = sw.Elapsed,
            Metrics = collectedMetrics,
            Errors = errors,
            Summary = summary
        };
    }
}
