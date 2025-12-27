using DamianH.HybridCacheHttpHandler;
using JetBrains.dotMemoryUnit;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace StressTests;

/// <summary>
/// Stress tests for HybridCacheHttpHandler using xUnit + dotMemoryUnit.
/// 
/// Prerequisites:
/// 1. Start infrastructure: cd stress-tests/AppHost && dotnet run
/// 2. Run tests: dotnet test
/// 
/// dotMemoryUnit will automatically capture memory snapshots and analyze for leaks.
/// </summary>
public class CacheStressTests : IDisposable
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HttpClient _cachedClient;
    private readonly HttpClient _uncachedClient;

    public CacheStressTests()
    {
        var targetUrl = new Uri(Environment.GetEnvironmentVariable("TARGET_URL") ?? "http://localhost:5001");

        // Setup HybridCache with Redis L2
        var services = new ServiceCollection();
        
        // Add Redis (connection string from environment or default)
        var redisConnection = Environment.GetEnvironmentVariable("ConnectionStrings__redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "StressTests:";
        });

        // Add HybridCache
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 10 * 1024 * 1024; // 10MB
            options.MaximumKeyLength = 1024;
            options.DefaultEntryOptions = new()
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            };
        });

        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var hybridCache = serviceProvider.GetRequiredService<HybridCache>();

        // Create cached client
        var cacheHandler = new HybridCacheHttpHandler(
            hybridCache,
            TimeProvider.System,
            new HybridCacheHttpHandlerOptions
            {
                DefaultCacheDuration = TimeSpan.FromMinutes(5),
                MaxCacheableContentSize = 10 * 1024 * 1024,
                CompressionThreshold = 1024,
                IncludeDiagnosticHeaders = true
            },
            serviceProvider.GetRequiredService<ILogger<HybridCacheHttpHandler>>())
        {
            InnerHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            }
        };

        _cachedClient = new HttpClient(cacheHandler)
        {
            BaseAddress = targetUrl
        };
        _uncachedClient = new HttpClient
        {
            BaseAddress = targetUrl
        };
    }

    [Fact]
    [DotMemoryUnit(CollectAllocations = true, FailIfRunWithoutSupport = false)]
    public async Task CacheStampede_ThunderingHerd_OnlyOneBackendCall()
    {
        // Arrange
        const string Url = "/api/delay/100?size=1024";
        const int Concurrency = 100;

        // Take baseline memory snapshot
        var memBefore = GC.GetTotalMemory(true);
        
        // Try to take dotMemory snapshot if available
        try
        {
            dotMemory.Check(_ =>
            {
                Console.WriteLine($"[dotMemoryUnit] Baseline snapshot taken");
            });
        }
        catch (Exception)
        {
            Console.WriteLine($"[GC] Baseline memory: {FormatBytes(memBefore)}");
        }

        // Act - Fire 100 concurrent requests to same endpoint
        var tasks = Enumerable.Range(0, Concurrency)
            .Select(async i =>
            {
                var response = await _cachedClient.GetAsync(Url, _ct);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(_ct);
                
                // Check for cache hit indicators
                var hasAge = response.Headers.Age is { TotalSeconds: > 0 };
                var hasCacheStatus = response.Headers.TryGetValues("X-Cache-Status", out _);
                var isCacheHit = hasAge || hasCacheStatus;
                
                // Debug first few responses
                if (i >= 3)
                {
                    return isCacheHit;
                }

                var cacheStatusValue = response.Headers.TryGetValues("X-Cache-Status", out var values) ? values.FirstOrDefault() : "none";
                Console.WriteLine($"  Request {i}: Age={response.Headers.Age?.TotalSeconds ?? 0}s, X-Cache-Status={cacheStatusValue}, CacheHit={isCacheHit}");

                return isCacheHit;
            })
            .ToArray();

        var cacheHits = await Task.WhenAll(tasks);

        // Take after-test memory snapshot
        var memAfter = GC.GetTotalMemory(false);
        
        try
        {
            dotMemory.Check(_ =>
            {
                Console.WriteLine($"[dotMemoryUnit] Final snapshot taken");
            });
        }
        catch (Exception)
        {
            Console.WriteLine($"[GC] Final memory: {FormatBytes(memAfter)}");
        }

        // Assert
        var hitRatio = cacheHits.Count(x => x) / (double)Concurrency;
        hitRatio.ShouldBeGreaterThan(0.95, $"Cache hit ratio should be >95%, was {hitRatio:P1}");

        // Memory assertion - no significant growth
        var memGrowth = memAfter - memBefore;
        memGrowth.ShouldBeLessThan(10 * 1024 * 1024, $"Memory growth should be <10MB, was {FormatBytes(memGrowth)}");

        Console.WriteLine($"✓ Cache Hit Ratio: {hitRatio:P1}");
        Console.WriteLine($"✓ Memory Growth: {FormatBytes(memGrowth)}");
    }

    [Fact]
    [DotMemoryUnit(CollectAllocations = true, FailIfRunWithoutSupport = false)]
    public async Task SustainedLoad_5Minutes_NoMemoryLeak()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);
        var concurrentClients = 20;
        var endpoints = new[]
        {
            "/api/cacheable/1?size=1024",
            "/api/cacheable/2?size=10240",
            "/api/sizes/small",
            "/api/sizes/medium"
        };

        var memStart = GC.GetTotalMemory(true);
        
        try
        {
            dotMemory.Check(_ => Console.WriteLine("[dotMemoryUnit] Baseline snapshot taken"));
        }
        catch (Exception)
        {
            Console.WriteLine($"[GC] Baseline memory: {FormatBytes(memStart)}");
        }

        var cts = new CancellationTokenSource(duration);
        var requestCount = 0;
        var errorCount = 0;

        // Act - Sustained load
        var tasks = Enumerable.Range(0, concurrentClients)
            .Select(async workerId =>
            {
                var random = new Random(workerId);
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var endpoint = endpoints[random.Next(endpoints.Length)];
                        var response = await _cachedClient.GetAsync(endpoint, cts.Token);
                        response.EnsureSuccessStatusCode();
                        Interlocked.Increment(ref requestCount);

                        // Take periodic snapshots
                        if (requestCount % 10000 == 0)
                        {
                            try
                            {
                                dotMemory.Check(memory => 
                                    Console.WriteLine($"[dotMemoryUnit] Snapshot at {requestCount:N0} requests"));
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"[GC] Checkpoint at {requestCount:N0} requests");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }

                    await Task.Delay(50, cts.Token); // 20 req/sec per client = 400 req/sec total
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        // Take final snapshot
        var memEnd = GC.GetTotalMemory(false);
        
        try
        {
            dotMemory.Check(_ => Console.WriteLine("[dotMemoryUnit] Final snapshot taken"));
        }
        catch (Exception)
        {
            Console.WriteLine($"[GC] Final memory: {FormatBytes(memEnd)}");
        }

        // Assert
        var memGrowth = memEnd - memStart;
        var errorRate = errorCount / (double)requestCount;

        errorRate.ShouldBeLessThan(0.01, $"Error rate should be <1%, was {errorRate:P1}");
        memGrowth.ShouldBeLessThan(50 * 1024 * 1024, $"Memory growth should be <50MB for 5min test, was {FormatBytes(memGrowth)}");

        Console.WriteLine($"✓ Total Requests: {requestCount:N0}");
        Console.WriteLine($"✓ Error Rate: {errorRate:P2}");
        Console.WriteLine($"✓ Memory Growth: {FormatBytes(memGrowth)}");
        Console.WriteLine($"✓ Throughput: {requestCount / duration.TotalSeconds:F1} req/s");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public void Dispose()
    {
        _cachedClient?.Dispose();
        _uncachedClient?.Dispose();
    }
}
