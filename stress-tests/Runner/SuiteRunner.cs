using Microsoft.Extensions.Logging;
using Runner.Config;
using Runner.Infrastructure;
using Runner.Suites;

namespace Runner;

/// <summary>
/// Non-interactive suite runner for Aspire orchestration.
/// Runs all test suites sequentially and reports results.
/// </summary>
public class SuiteRunner(
    IEnumerable<ISuite> suites,
    CachedClientFactory clientFactory,
    ILogger<SuiteRunner> logger)
{
    public async Task<int> RunAllSuitesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("=".PadRight(70, '='));
        logger.LogInformation("HybridCacheHttpHandler Stress Test Runner");
        logger.LogInformation("=".PadRight(70, '='));
        logger.LogInformation("");

        var config = new SuiteConfig
        {
            IncludeDiagnostics = true
        };

        var suiteList = suites.ToList();
        logger.LogInformation("Found {Count} test suite(s) to run", suiteList.Count);
        logger.LogInformation("");

        var results = new List<(ISuite Suite, SuiteResult Result)>();
        var overallSuccess = true;

        foreach (var suite in suiteList)
        {
            logger.LogInformation("Starting: {Name}", suite.Name);
            logger.LogInformation("  Description: {Description}", suite.Description.Split('\n')[0]);
            logger.LogInformation("  Estimated Duration: {Duration}", suite.EstimatedDuration);
            logger.LogInformation("");

            try
            {
                using var client = clientFactory.CreateClient();
                
                var progress = new Progress<SuiteProgress>(p =>
                {
                    if (!string.IsNullOrEmpty(p.CurrentPhase))
                    {
                        logger.LogInformation("  [{Phase}] {Completed}/{Total} requests - Cache: {Hits} hits, {Misses} misses - Avg: {Latency:F1}ms",
                            p.CurrentPhase,
                            p.CompletedRequests,
                            p.TotalRequests,
                            p.CacheHits,
                            p.CacheMisses,
                            p.AverageLatencyMs);
                    }
                });

                var result = await suite.RunAsync(client, config, progress, cancellationToken);
                results.Add((suite, result));

                if (result.Success)
                {
                    logger.LogInformation("✓ PASSED: {Name} ({Duration:F2}s)", suite.Name, result.Duration.TotalSeconds);
                }
                else
                {
                    logger.LogError("✗ FAILED: {Name} ({Duration:F2}s)", suite.Name, result.Duration.TotalSeconds);
                    overallSuccess = false;
                }

                // Log summary
                logger.LogInformation("  Summary: {Summary}", result.Summary.Split('\n')[0]);
                logger.LogInformation("  Cache Hit Ratio: {Ratio:P1}", result.Metrics.CacheHitRatio);
                logger.LogInformation("  Latency P95: {P95:F2}ms", result.Metrics.Latency.P95Ms);
                logger.LogInformation("  Memory Peak: {Memory:F2} MB", result.Metrics.Memory.PeakBytes / 1024.0 / 1024.0);
                logger.LogInformation("  Gen2 Collections: {Gen2}", result.Metrics.Memory.Gen2Collections);

                if (result.Errors.Any())
                {
                    logger.LogWarning("  Errors: {Count}", result.Errors.Count);
                    foreach (var error in result.Errors.Take(3))
                    {
                        logger.LogWarning("    - {Error}", error);
                    }
                    if (result.Errors.Count > 3)
                    {
                        logger.LogWarning("    ... and {More} more", result.Errors.Count - 3);
                    }
                }

                logger.LogInformation("");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Suite {Name} threw exception", suite.Name);
                overallSuccess = false;
                logger.LogInformation("");
            }
        }

        // Print summary table
        logger.LogInformation("=".PadRight(70, '='));
        logger.LogInformation("Test Summary");
        logger.LogInformation("=".PadRight(70, '='));
        logger.LogInformation("");
        logger.LogInformation("{0,-40} {1,-10} {2,-10}", "Suite", "Status", "Duration");
        logger.LogInformation("-".PadRight(70, '-'));

        foreach (var (suite, result) in results)
        {
            var status = result.Success ? "✓ PASSED" : "✗ FAILED";
            logger.LogInformation("{0,-40} {1,-10} {2,-10:F2}s", 
                suite.Name.Length > 40 ? suite.Name[..37] + "..." : suite.Name,
                status,
                result.Duration.TotalSeconds);
        }

        logger.LogInformation("");
        logger.LogInformation("Total: {Total} suite(s), {Passed} passed, {Failed} failed",
            results.Count,
            results.Count(r => r.Result.Success),
            results.Count(r => !r.Result.Success));

        logger.LogInformation("=".PadRight(70, '='));
        logger.LogInformation("");

        // Return exit code: 0 = success, 1 = failure
        return overallSuccess ? 0 : 1;
    }
}
