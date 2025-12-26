using System.Text.Json;
using JetBrains.dotMemoryUnit;

namespace Runner.Infrastructure;

/// <summary>
/// Information about a memory snapshot.
/// </summary>
public record SnapshotInfo
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int RequestCount { get; init; }
    public long MemoryBytes { get; init; }
    public string? FilePath { get; init; }
}

/// <summary>
/// Manages memory snapshot lifecycle during stress tests.
/// </summary>
public class SnapshotManager : IDisposable
{
    private readonly string _outputPath;
    private readonly int _maxSnapshots;
    private readonly List<SnapshotInfo> _snapshots = [];
    private readonly string _runId;
    private int _snapshotCounter;
    private bool _isInitialized;

    public IReadOnlyList<SnapshotInfo> Snapshots => _snapshots.AsReadOnly();

    public SnapshotManager(string outputPath, int maxSnapshots)
    {
        _outputPath = outputPath;
        _maxSnapshots = maxSnapshots;
        _runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        
        Directory.CreateDirectory(GetRunDirectory());
    }

    /// <summary>
    /// Initialize dotMemoryUnit profiling.
    /// </summary>
    public void Initialize(bool trackAllocations)
    {
        if (_isInitialized)
            return;

        try
        {
            // dotMemoryUnit 3.x automatically saves to workspace directory
            // We'll manually manage snapshot files via dotMemory.Check()
            _isInitialized = true;
            Console.WriteLine($"[MemoryProfiler] Initialized. Snapshots will be analyzed in-memory.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize dotMemoryUnit: {ex.Message}");
            Console.WriteLine("Continuing without memory profiling...");
        }
    }

    /// <summary>
    /// Take a snapshot with the given label.
    /// </summary>
    public SnapshotInfo TakeSnapshot(string label, int requestCount)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SnapshotManager not initialized. Call Initialize() first.");
        }

        var snapshotId = $"snapshot-{++_snapshotCounter:D3}";
        var memoryBytes = GC.GetTotalMemory(false);

        try
        {
            // Force GC before snapshot for cleaner results
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Take dotMemory snapshot
            dotMemory.Check(memory =>
            {
                // This block executes with snapshot context
                Console.WriteLine($"[MemoryProfiler] Snapshot {snapshotId} taken: {label}");
            });

            var snapshot = new SnapshotInfo
            {
                Id = snapshotId,
                Label = label,
                Timestamp = DateTime.UtcNow,
                RequestCount = requestCount,
                MemoryBytes = memoryBytes,
                FilePath = Path.Combine(GetRunDirectory(), $"{snapshotId}.dmw")
            };

            _snapshots.Add(snapshot);

            // Enforce max snapshots limit
            if (_snapshots.Count > _maxSnapshots)
            {
                var toRemove = _snapshots[0];
                _snapshots.RemoveAt(0);
                
                if (File.Exists(toRemove.FilePath))
                {
                    try
                    {
                        File.Delete(toRemove.FilePath);
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to take snapshot: {ex.Message}");
            
            // Return a snapshot info without file path
            return new SnapshotInfo
            {
                Id = snapshotId,
                Label = label,
                Timestamp = DateTime.UtcNow,
                RequestCount = requestCount,
                MemoryBytes = memoryBytes
            };
        }
    }

    /// <summary>
    /// Save snapshot metadata to JSON.
    /// </summary>
    public async Task SaveMetadataAsync()
    {
        var metadataPath = Path.Combine(GetRunDirectory(), "snapshots.json");
        
        var json = JsonSerializer.Serialize(_snapshots, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(metadataPath, json);
    }

    private string GetRunDirectory()
    {
        return Path.Combine(_outputPath, _runId);
    }

    public void Dispose()
    {
        try
        {
            SaveMetadataAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save snapshot metadata: {ex.Message}");
        }
    }
}
