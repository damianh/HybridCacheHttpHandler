using System.Diagnostics;
using Runner.Config;
using Runner.Infrastructure;

namespace Runner.Suites;

/// <summary>
/// Long-running sustained load test designed to detect memory leaks.
/// Runs for 5+ minutes with consistent traffic to identify gradual memory growth.
/// </summary>
public class MemoryLeakDetectionSuite : ISuite
{
    private readonly MemoryProfilingConfig _memoryConfig;

    public MemoryLeakDetectionSuite(MemoryProfilingConfig memoryConfig)
    {
        _memoryConfig = memoryConfig;
    }

    public string Name => "Memory Leak Detection Test";
    
    public string Description => 
        "Sustained load for 5 minutes with varied request patterns\n" +
        "Validates: No memory leaks, stable memory usage, reasonable GC pressure\n" +
        "Takes snapshots every 10K requests for analysis";
    
    public TimeSpan EstimatedDuration => TimeSpan.FromMinutes(5);

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
        
        // Test configuration
        var testDuration = TimeSpan.FromMinutes(5);
        var concurrentClients = 20;
        var targetRps = 100; // requests per second
        var delayBetweenRequests = TimeSpan.FromMilliseconds(1000.0 / targetRps * concurrentClients);

        // Endpoints to cycle through (realistic mixed workload)
        var endpoints = new[]
        {
            "/api/cacheable/1?size=1024",      // Small cacheable
            "/api/cacheable/2?size=10240",     // Medium cacheable
            "/api/cacheable/3?size=102400",    // Large cacheable
            "/api/sizes/small",                 // Small predefined
            "/api/sizes/medium",                // Medium predefined
        };

        progress.Report(new SuiteProgress
        {
            TotalRequests = 0, // Unknown upfront
            CurrentPhase = "Phase 1: Warmup (1 min)"
        });

        var endTime = DateTime.UtcNow.Add(testDuration);
        var phaseStart = DateTime.UtcNow;
        var currentPhase = 1;
        
        // Worker tasks for concurrent clients
        var workerTasks = Enumerable.Range(0, concurrentClients)
            .Select(async workerId =>
            {
                var random = new Random(workerId);
                
                while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
                {
                    try
                    {
                        // Select random endpoint
                        var endpoint = endpoints[random.Next(endpoints.Length)];
                        
                        var requestSw = Stopwatch.StartNew();
                        var response = await client.GetAsync(endpoint, ct);
                        requestSw.Stop();

                        var cacheHit = response.Headers.Age?.TotalSeconds > 0 ||
                                      response.Headers.Contains("X-Cache-Status");

                        metrics.RecordRequest(
                            success: response.IsSuccessStatusCode,
                            latencyMs: requestSw.ElapsedMilliseconds,
                            cacheHit: cacheHit
                        );

                        // Update phase based on elapsed time
                        var elapsed = DateTime.UtcNow - phaseStart;
                        var newPhase = elapsed.TotalMinutes switch
                        {
                            < 1 => 1,  // Warmup
                            < 4 => 2,  // Steady state
                            _ => 3     // Cooldown
                        };

                        if (newPhase != currentPhase)
                        {
                            currentPhase = newPhase;
                            var phaseName = currentPhase switch
                            {
                                1 => "Warmup",
                                2 => "Steady State",
                                3 => "Cooldown",
                                _ => "Unknown"
                            };
                            
                            memoryProfiler.TakeSnapshot($"phase-{currentPhase}-{phaseName}", metrics.TotalRequests);
                        }

                        // Progress reporting (throttled to avoid overhead)
                        if (metrics.TotalRequests % 100 == 0)
                        {
                            var phaseDesc = currentPhase switch
                            {
                                1 => "Phase 1: Warmup (1 min)",
                                2 => "Phase 2: Steady State (3 mins)",
                                3 => "Phase 3: Cooldown",
                                _ => "Running..."
                            };

                            progress.Report(new SuiteProgress
                            {
                                CompletedRequests = metrics.TotalRequests,
                                TotalRequests = (int)(testDuration.TotalSeconds * targetRps), // Estimate
                                CacheHits = metrics.CacheHits,
                                CacheMisses = metrics.CacheMisses,
                                AverageLatencyMs = metrics.AverageLatencyMs,
                                CurrentPhase = phaseDesc,
                                ErrorCount = errors.Count
                            });
                        }

                        // Rate limiting
                        await Task.Delay(delayBetweenRequests, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            if (errors.Count < 100) // Cap error collection
                            {
                                errors.Add(ex.Message);
                            }
                        }
                    }
                }
            })
            .ToArray();

        await Task.WhenAll(workerTasks);
        
        // Stop memory profiling and analyze
        progress.Report(new SuiteProgress
        {
            CurrentPhase = "Analyzing memory snapshots..."
        });
        
        var memoryReport = await memoryProfiler.StopAndAnalyzeAsync(metrics.TotalRequests);
        
        sw.Stop();

        var collectedMetrics = metrics.GetMetrics();
        
        // Calculate throughput
        collectedMetrics = collectedMetrics with
        {
            Throughput = collectedMetrics.Throughput with
            {
                RequestsPerSecond = collectedMetrics.TotalRequests / sw.Elapsed.TotalSeconds
            }
        };

        // Validate results
        var success = errors.Count < 10 && // Allow some errors
                     collectedMetrics.SuccessfulRequests > collectedMetrics.TotalRequests * 0.99; // 99% success rate

        // Memory leak detection
        var memoryIssues = new List<string>();
        
        if (memoryReport.HasMemoryLeak)
        {
            memoryIssues.Add($"⚠️ MEMORY LEAK DETECTED:");
            foreach (var leak in memoryReport.PotentialLeaks)
            {
                memoryIssues.Add($"  - [{leak.Severity}] {leak.Type}: {leak.Description}");
            }
            
            if (_memoryConfig.FailOnLeakDetection)
            {
                success = false;
            }
        }

        if (memoryReport.Warnings.Count > 0)
        {
            memoryIssues.Add("Warnings:");
            memoryIssues.AddRange(memoryReport.Warnings.Select(w => $"  - {w}"));
        }

        // Build summary
        var summaryParts = new List<string>();
        
        if (success)
        {
            summaryParts.Add("✓ Sustained load completed successfully");
        }
        else
        {
            summaryParts.Add("✗ Test failed due to errors or memory leaks");
        }

        summaryParts.Add($"Total requests: {collectedMetrics.TotalRequests:N0}");
        summaryParts.Add($"Cache hit ratio: {collectedMetrics.CacheHitRatio:P1}");
        summaryParts.Add($"Throughput: {collectedMetrics.Throughput.RequestsPerSecond:F1} req/s");
        summaryParts.Add($"Memory growth: {FormatBytes(memoryReport.GrowthAnalysis.GrowthBytes)} ({memoryReport.GrowthAnalysis.GrowthPercentage:F1}%)");
        summaryParts.Add($"Gen2 collections: {memoryReport.GrowthAnalysis.Gen2Collections}");
        summaryParts.Add($"Snapshots taken: {memoryReport.TotalSnapshots}");

        if (!memoryReport.HasMemoryLeak)
        {
            summaryParts.Add("✓ No memory leaks detected");
        }

        if (memoryIssues.Count > 0)
        {
            summaryParts.Add("");
            summaryParts.AddRange(memoryIssues);
        }

        errors.AddRange(memoryIssues);

        return new SuiteResult
        {
            Success = success,
            Duration = sw.Elapsed,
            Metrics = collectedMetrics,
            Errors = errors,
            Summary = string.Join("\n", summaryParts)
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
