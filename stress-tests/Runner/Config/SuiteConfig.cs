namespace Runner.Config;

public record SuiteConfig
{
    public int ConcurrentClients { get; init; } = 50;
    public int TotalRequests { get; init; } = 1000;
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);
    public string TargetUrl { get; init; } = "http://localhost:5001";
    public bool IncludeDiagnostics { get; init; } = true;
}

public record SuiteProgress
{
    public int CompletedRequests { get; init; }
    public int TotalRequests { get; init; }
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
    public double AverageLatencyMs { get; init; }
    public int ErrorCount { get; init; }
    public string CurrentPhase { get; init; } = "";
}

public record SuiteResult
{
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
    public SuiteMetrics Metrics { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public string Summary { get; init; } = "";
}

public record SuiteMetrics
{
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
    public double CacheHitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public LatencyStats Latency { get; init; } = new();
    public MemoryStats Memory { get; init; } = new();
    public ThroughputStats Throughput { get; init; } = new();
}

public record LatencyStats
{
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double MeanMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
}

public record MemoryStats
{
    public long StartBytes { get; init; }
    public long PeakBytes { get; init; }
    public long EndBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
}

public record ThroughputStats
{
    public double RequestsPerSecond { get; init; }
    public double BytesPerSecond { get; init; }
}
