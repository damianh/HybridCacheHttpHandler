namespace Runner.Config;

public record MemoryProfilingConfig
{
    /// <summary>
    /// Take a snapshot every N requests (deterministic trigger).
    /// </summary>
    public int SnapshotEveryNRequests { get; init; } = 10_000;

    /// <summary>
    /// Maximum number of snapshots to keep (prevent disk bloat).
    /// </summary>
    public int MaxSnapshots { get; init; } = 20;

    /// <summary>
    /// Output directory for snapshots and reports.
    /// </summary>
    public string OutputPath { get; init; } = "./profiles";

    /// <summary>
    /// Threshold for leak detection: memory growth in bytes over duration.
    /// </summary>
    public long LeakThresholdBytes { get; init; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Threshold for Gen2 collection warnings.
    /// </summary>
    public int Gen2CollectionThreshold { get; init; } = 5;

    /// <summary>
    /// Track allocations (adds overhead but provides detailed data).
    /// </summary>
    public bool TrackAllocations { get; init; } = true;

    /// <summary>
    /// Generate JSON report after profiling.
    /// </summary>
    public bool GenerateJsonReport { get; init; } = true;

    /// <summary>
    /// Generate HTML report after profiling.
    /// </summary>
    public bool GenerateHtmlReport { get; init; } = true;

    /// <summary>
    /// Fail the test run if memory leak is detected.
    /// </summary>
    public bool FailOnLeakDetection { get; init; } = false;
}
