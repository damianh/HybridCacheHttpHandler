using Runner.Config;

namespace Runner.Infrastructure;

public class MetricsCollector
{
    private readonly List<double> _latencies = [];
    private readonly Lock _lock = new();
    private long _startMemory;
    private long _peakMemory;
    private int _gen0Start, _gen1Start, _gen2Start;
    
    public int TotalRequests { get; private set; }
    public int SuccessfulRequests { get; private set; }
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }
    public double AverageLatencyMs => _latencies.Count > 0 ? _latencies.Average() : 0;

    public MetricsCollector()
        => StartMemoryTracking();

    private void StartMemoryTracking()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _startMemory = GC.GetTotalMemory(false);
        _peakMemory = _startMemory;
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
    }

    public void RecordRequest(bool success, double latencyMs, bool cacheHit)
    {
        lock (_lock)
        {
            TotalRequests++;
            if (success)
            {
                SuccessfulRequests++;
            }
            if (cacheHit)
            {
                CacheHits++;
            }
            else
            {
                CacheMisses++;
            }

            _latencies.Add(latencyMs);

            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > _peakMemory)
            {
                _peakMemory = currentMemory;
            }
        }
    }

    public SuiteMetrics GetMetrics()
    {
        lock (_lock)
        {
            var sortedLatencies = _latencies.OrderBy(x => x).ToList();
            
            return new SuiteMetrics
            {
                TotalRequests = TotalRequests,
                SuccessfulRequests = SuccessfulRequests,
                CacheHits = CacheHits,
                CacheMisses = CacheMisses,
                Latency = new LatencyStats
                {
                    MinMs = sortedLatencies.Count > 0 ? sortedLatencies.First() : 0,
                    MaxMs = sortedLatencies.Count > 0 ? sortedLatencies.Last() : 0,
                    MeanMs = AverageLatencyMs,
                    P50Ms = GetPercentile(sortedLatencies, 0.50),
                    P95Ms = GetPercentile(sortedLatencies, 0.95),
                    P99Ms = GetPercentile(sortedLatencies, 0.99)
                },
                Memory = new MemoryStats
                {
                    StartBytes = _startMemory,
                    PeakBytes = _peakMemory,
                    EndBytes = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0) - _gen0Start,
                    Gen1Collections = GC.CollectionCount(1) - _gen1Start,
                    Gen2Collections = GC.CollectionCount(2) - _gen2Start
                },
                Throughput = new ThroughputStats
                {
                    RequestsPerSecond = 0, // Calculated by caller
                    BytesPerSecond = 0
                }
            };
        }
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(index, sortedValues.Count - 1));
        return sortedValues[index];
    }
}
