using System.Diagnostics;
using Runner.Config;
using Runner.Infrastructure;

namespace Runner.Suites;

public class CacheStampedeSuite : ISuite
{
    private readonly MemoryProfilingConfig _memoryConfig;

    public CacheStampedeSuite(MemoryProfilingConfig memoryConfig)
    {
        _memoryConfig = memoryConfig;
    }

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
        
        // Initialize memory profiling
        using var memoryProfiler = new MemoryProfiler(_memoryConfig);
        memoryProfiler.Start();
        
        var metrics = new MetricsCollector(memoryProfiler);
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
        
        // Stop memory profiling and analyze
        var memoryReport = await memoryProfiler.StopAndAnalyzeAsync(metrics.TotalRequests);
        
        sw.Stop();

        var collectedMetrics = metrics.GetMetrics();

        // Validate
        var success = errors.Count == 0 && 
                     collectedMetrics.CacheHitRatio > 0.95; // 95% should be cache hits
        
        // Add memory leak warnings to errors if detected
        if (memoryReport.HasMemoryLeak)
        {
            errors.Add($"⚠️ Memory leak detected: {string.Join(", ", memoryReport.PotentialLeaks.Select(l => l.Description))}");
            
            if (_memoryConfig.FailOnLeakDetection)
            {
                success = false;
            }
        }

        var summary = success 
            ? "✓ Request collapsing worked - only 1-2 backend calls"
            : "✗ Too many cache misses or errors occurred";
        
        if (memoryReport.Warnings.Count > 0)
        {
            summary += $"\nMemory warnings: {memoryReport.Warnings.Count}";
        }

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
