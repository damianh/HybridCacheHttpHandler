using Runner.Config;
using System.Text.Json;

namespace Runner.Infrastructure;

/// <summary>
/// Result of memory profiling analysis.
/// </summary>
public record MemoryAnalysisReport
{
    public required string RunId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required int TotalSnapshots { get; init; }
    public required List<SnapshotInfo> Snapshots { get; init; }
    public required MemoryGrowthAnalysis GrowthAnalysis { get; init; }
    public required List<string> Warnings { get; init; }
    public required List<LeakCandidate> PotentialLeaks { get; init; }
    public required bool HasMemoryLeak { get; init; }
}

public record MemoryGrowthAnalysis
{
    public long StartBytes { get; init; }
    public long EndBytes { get; init; }
    public long GrowthBytes { get; init; }
    public double GrowthPercentage { get; init; }
    public int Gen2Collections { get; init; }
}

public record LeakCandidate
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }
}

/// <summary>
/// Orchestrates memory profiling for stress tests.
/// </summary>
public class MemoryProfiler : IDisposable
{
    private readonly MemoryProfilingConfig _config;
    private readonly SnapshotManager _snapshotManager;
    private int _lastSnapshotRequestCount;
    private bool _isRunning;

    public MemoryProfiler(MemoryProfilingConfig config)
    {
        _config = config;
        _snapshotManager = new SnapshotManager(config.OutputPath, config.MaxSnapshots);
    }

    /// <summary>
    /// Start memory profiling session.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _snapshotManager.Initialize(_config.TrackAllocations);
        _isRunning = true;

        // Take baseline snapshot
        _snapshotManager.TakeSnapshot("baseline", 0);
        _lastSnapshotRequestCount = 0;

        Console.WriteLine($"[MemoryProfiler] Started profiling. Snapshots will be saved to: {_config.OutputPath}");
        Console.WriteLine($"[MemoryProfiler] Taking snapshot every {_config.SnapshotEveryNRequests:N0} requests");
    }

    /// <summary>
    /// Check if a snapshot should be taken based on request count.
    /// </summary>
    public void CheckAndTakeSnapshot(int currentRequestCount)
    {
        if (!_isRunning)
            return;

        if (currentRequestCount - _lastSnapshotRequestCount >= _config.SnapshotEveryNRequests)
        {
            var label = $"after-{currentRequestCount:N0}-requests";
            _snapshotManager.TakeSnapshot(label, currentRequestCount);
            _lastSnapshotRequestCount = currentRequestCount;
        }
    }

    /// <summary>
    /// Take a named snapshot (e.g., at test phase boundaries).
    /// </summary>
    public void TakeSnapshot(string label, int requestCount)
    {
        if (!_isRunning)
            return;

        _snapshotManager.TakeSnapshot(label, requestCount);
    }

    /// <summary>
    /// Stop profiling and analyze results.
    /// </summary>
    public async Task<MemoryAnalysisReport> StopAndAnalyzeAsync(int finalRequestCount)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Profiler not running");

        // Take final snapshot
        _snapshotManager.TakeSnapshot("final", finalRequestCount);
        _isRunning = false;

        // Analyze snapshots
        var report = AnalyzeSnapshots();

        // Generate reports
        if (_config.GenerateJsonReport)
        {
            await SaveJsonReportAsync(report);
        }

        if (_config.GenerateHtmlReport)
        {
            await SaveHtmlReportAsync(report);
        }

        return report;
    }

    private MemoryAnalysisReport AnalyzeSnapshots()
    {
        var snapshots = _snapshotManager.Snapshots.ToList();
        var warnings = new List<string>();
        var leaks = new List<LeakCandidate>();

        if (snapshots.Count < 2)
        {
            warnings.Add("Insufficient snapshots for analysis (need at least 2)");
            
            return new MemoryAnalysisReport
            {
                RunId = GetRunId(),
                TotalSnapshots = snapshots.Count,
                Snapshots = snapshots,
                GrowthAnalysis = new MemoryGrowthAnalysis(),
                Warnings = warnings,
                PotentialLeaks = leaks,
                HasMemoryLeak = false
            };
        }

        var firstSnapshot = snapshots[0];
        var lastSnapshot = snapshots[^1];
        
        var growthBytes = lastSnapshot.MemoryBytes - firstSnapshot.MemoryBytes;
        var growthPercentage = firstSnapshot.MemoryBytes > 0 
            ? (growthBytes / (double)firstSnapshot.MemoryBytes) * 100 
            : 0;

        var growthAnalysis = new MemoryGrowthAnalysis
        {
            StartBytes = firstSnapshot.MemoryBytes,
            EndBytes = lastSnapshot.MemoryBytes,
            GrowthBytes = growthBytes,
            GrowthPercentage = growthPercentage,
            Gen2Collections = GC.CollectionCount(2)
        };

        // Detect potential memory leaks
        var hasLeak = false;

        // Check absolute growth
        if (growthBytes > _config.LeakThresholdBytes)
        {
            leaks.Add(new LeakCandidate
            {
                Type = "AbsoluteGrowth",
                Description = $"Memory grew by {FormatBytes(growthBytes)} (threshold: {FormatBytes(_config.LeakThresholdBytes)})",
                Severity = "High"
            });
            hasLeak = true;
        }

        // Check linear growth pattern (each snapshot growing)
        var growthPattern = AnalyzeGrowthPattern(snapshots);
        if (growthPattern.IsLinearGrowth && growthPattern.AverageGrowthPerSnapshot > 1_000_000) // 1MB per snapshot
        {
            leaks.Add(new LeakCandidate
            {
                Type = "LinearGrowth",
                Description = $"Memory growing linearly: ~{FormatBytes((long)growthPattern.AverageGrowthPerSnapshot)} per snapshot",
                Severity = "High"
            });
            hasLeak = true;
        }

        // Check Gen2 collections
        if (growthAnalysis.Gen2Collections > _config.Gen2CollectionThreshold)
        {
            warnings.Add($"High Gen2 collections: {growthAnalysis.Gen2Collections} (threshold: {_config.Gen2CollectionThreshold})");
            
            if (growthAnalysis.Gen2Collections > _config.Gen2CollectionThreshold * 2)
            {
                leaks.Add(new LeakCandidate
                {
                    Type = "ExcessiveGen2",
                    Description = $"Excessive Gen2 collections: {growthAnalysis.Gen2Collections}",
                    Severity = "Medium"
                });
            }
        }

        // Check percentage growth
        if (growthPercentage > 50)
        {
            warnings.Add($"Memory grew by {growthPercentage:F1}% during test");
        }

        return new MemoryAnalysisReport
        {
            RunId = GetRunId(),
            TotalSnapshots = snapshots.Count,
            Snapshots = snapshots,
            GrowthAnalysis = growthAnalysis,
            Warnings = warnings,
            PotentialLeaks = leaks,
            HasMemoryLeak = hasLeak
        };
    }

    private static (bool IsLinearGrowth, double AverageGrowthPerSnapshot) AnalyzeGrowthPattern(List<SnapshotInfo> snapshots)
    {
        if (snapshots.Count < 3)
            return (false, 0);

        var growthDeltas = new List<long>();
        for (int i = 1; i < snapshots.Count; i++)
        {
            var growth = snapshots[i].MemoryBytes - snapshots[i - 1].MemoryBytes;
            growthDeltas.Add(growth);
        }

        // Check if growth is consistently positive (linear pattern)
        var positiveGrowthCount = growthDeltas.Count(g => g > 0);
        var isLinearGrowth = positiveGrowthCount >= growthDeltas.Count * 0.8; // 80% of snapshots show growth

        var averageGrowth = growthDeltas.Average();

        return (isLinearGrowth, averageGrowth);
    }

    private async Task SaveJsonReportAsync(MemoryAnalysisReport report)
    {
        var reportPath = Path.Combine(_config.OutputPath, GetRunId(), "analysis-report.json");
        
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(reportPath, json);
        Console.WriteLine($"[MemoryProfiler] JSON report saved: {reportPath}");
    }

    private async Task SaveHtmlReportAsync(MemoryAnalysisReport report)
    {
        var reportPath = Path.Combine(_config.OutputPath, GetRunId(), "analysis-report.html");
        
        var html = GenerateHtmlReport(report);
        await File.WriteAllTextAsync(reportPath, html);
        
        Console.WriteLine($"[MemoryProfiler] HTML report saved: {reportPath}");
    }

    private static string GenerateHtmlReport(MemoryAnalysisReport report)
    {
        var leakStatus = report.HasMemoryLeak ? "⚠️ MEMORY LEAK DETECTED" : "✅ NO LEAKS DETECTED";
        var statusColor = report.HasMemoryLeak ? "#dc3545" : "#28a745";

        var snapshotsTable = string.Join("\n", report.Snapshots.Select(s => $@"
            <tr>
                <td>{s.Id}</td>
                <td>{s.Label}</td>
                <td>{s.RequestCount:N0}</td>
                <td>{FormatBytes(s.MemoryBytes)}</td>
                <td>{s.Timestamp:yyyy-MM-dd HH:mm:ss}</td>
            </tr>"));

        var leaksTable = report.PotentialLeaks.Count > 0 
            ? string.Join("\n", report.PotentialLeaks.Select(leak => $@"
                <tr>
                    <td><span class='severity-{leak.Severity.ToLower()}'>{leak.Severity}</span></td>
                    <td>{leak.Type}</td>
                    <td>{leak.Description}</td>
                </tr>"))
            : "<tr><td colspan='3' style='text-align:center'>No memory leaks detected</td></tr>";

        var warningsSection = report.Warnings.Count > 0
            ? $"<div class='warnings'><h3>⚠️ Warnings</h3><ul>{string.Join("", report.Warnings.Select(w => $"<li>{w}</li>"))}</ul></div>"
            : "";

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Memory Analysis Report - {report.RunId}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; border-bottom: 3px solid #007bff; padding-bottom: 10px; }}
        h2 {{ color: #555; margin-top: 30px; }}
        .status {{ padding: 15px; border-radius: 5px; margin: 20px 0; font-size: 18px; font-weight: bold; background: {statusColor}; color: white; }}
        .metric {{ background: #f8f9fa; padding: 15px; margin: 10px 0; border-left: 4px solid #007bff; }}
        .metric strong {{ display: inline-block; width: 200px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background: #007bff; color: white; font-weight: 600; }}
        tr:hover {{ background: #f5f5f5; }}
        .severity-high {{ color: #dc3545; font-weight: bold; }}
        .severity-medium {{ color: #fd7e14; font-weight: bold; }}
        .severity-low {{ color: #ffc107; font-weight: bold; }}
        .warnings {{ background: #fff3cd; border: 1px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 5px; }}
        .warnings ul {{ margin: 10px 0; }}
        .footer {{ text-align: center; color: #666; margin-top: 40px; padding-top: 20px; border-top: 1px solid #ddd; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Memory Analysis Report</h1>
        <p><strong>Run ID:</strong> {report.RunId}</p>
        <p><strong>Generated:</strong> {report.Timestamp:yyyy-MM-dd HH:mm:ss UTC}</p>
        
        <div class='status'>{leakStatus}</div>

        <h2>Memory Growth Analysis</h2>
        <div class='metric'><strong>Start Memory:</strong> {FormatBytes(report.GrowthAnalysis.StartBytes)}</div>
        <div class='metric'><strong>End Memory:</strong> {FormatBytes(report.GrowthAnalysis.EndBytes)}</div>
        <div class='metric'><strong>Growth:</strong> {FormatBytes(report.GrowthAnalysis.GrowthBytes)} ({report.GrowthAnalysis.GrowthPercentage:F1}%)</div>
        <div class='metric'><strong>Gen2 Collections:</strong> {report.GrowthAnalysis.Gen2Collections}</div>

        {warningsSection}

        <h2>Potential Memory Leaks</h2>
        <table>
            <tr>
                <th>Severity</th>
                <th>Type</th>
                <th>Description</th>
            </tr>
            {leaksTable}
        </table>

        <h2>Snapshots ({report.TotalSnapshots})</h2>
        <table>
            <tr>
                <th>ID</th>
                <th>Label</th>
                <th>Requests</th>
                <th>Memory</th>
                <th>Timestamp</th>
            </tr>
            {snapshotsTable}
        </table>

        <div class='footer'>
            Generated by HybridCacheHttpHandler Stress Test Runner
        </div>
    </div>
</body>
</html>";
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

    private string GetRunId()
    {
        return _snapshotManager.Snapshots.FirstOrDefault()?.FilePath
            ?.Split(Path.DirectorySeparatorChar)[^2] 
            ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    }

    public void Dispose()
    {
        _snapshotManager.Dispose();
    }
}
