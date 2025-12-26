# Memory Profiling with dotMemoryUnit

This directory contains automated memory profiling results from stress test runs.

## Overview

Memory profiling is **always enabled** for stress tests to detect memory leaks automatically. The system uses JetBrains dotMemoryUnit to capture snapshots at regular intervals (every 10,000 requests) and analyze memory growth patterns.

## Directory Structure

```
profiles/
├── 20251226-115643/              # Run ID (timestamp)
│   ├── snapshot-001.dmw          # Baseline snapshot
│   ├── snapshot-002.dmw          # After 10K requests
│   ├── snapshot-003.dmw          # After 20K requests
│   ├── ...
│   ├── snapshots.json            # Snapshot metadata
│   ├── analysis-report.json      # Machine-readable analysis
│   └── analysis-report.html      # Human-readable report
└── 20251226-120145/              # Another run
    └── ...
```

## Files

- **snapshot-NNN.dmw**: dotMemory snapshot files (can be opened in dotMemory UI)
- **snapshots.json**: Metadata about each snapshot (timestamp, request count, memory size)
- **analysis-report.json**: Automated analysis results in JSON format
- **analysis-report.html**: Visual report with charts and leak detection results

## Configuration

Edit `Runner/Config/MemoryProfilingConfig.cs` to adjust:

```csharp
public record MemoryProfilingConfig
{
    // Take snapshot every N requests (deterministic)
    public int SnapshotEveryNRequests { get; init; } = 10_000;

    // Memory growth threshold for leak detection
    public long LeakThresholdBytes { get; init; } = 10 * 1024 * 1024; // 10MB

    // Fail the test if leak detected
    public bool FailOnLeakDetection { get; init; } = false;
    
    // ... more options
}
```

## Leak Detection

The system automatically detects:

1. **Absolute Growth**: Memory grows > 10MB during test
2. **Linear Growth**: Memory increases consistently across snapshots
3. **Excessive Gen2 Collections**: > 5 Gen2 GCs (configurable)
4. **High Growth Percentage**: > 50% memory increase

## Analyzing Results

### Option 1: View HTML Report (Easiest)
```bash
# Open the generated HTML report
start profiles/20251226-115643/analysis-report.html
```

### Option 2: View JSON Report (CI/CD)
```bash
# Parse JSON for automation
cat profiles/20251226-115643/analysis-report.json | jq '.HasMemoryLeak'
```

### Option 3: Open Snapshots in dotMemory (Most Detailed)
1. Open dotMemory
2. File → Open → Select any `.dmw` file
3. Compare snapshots to see object growth
4. Use built-in leak detection features

## Debugging Memory Issues

### Attach dotMemory to Running Process

```powershell
# Find the Runner process ID
Get-Process Runner

# Attach dotMemory
dotMemory.exe attach <PID> --save-to=.\manual-profile
```

### Set Breakpoints in Suites

```csharp
public async Task<SuiteResult> RunAsync(...)
{
    using var memoryProfiler = new MemoryProfiler(_memoryConfig);
    memoryProfiler.Start();
    
    // Set breakpoint here - profiler is active
    var baseline = memoryProfiler.TakeSnapshot("before-leak", 0);
    
    // Run problematic code
    await RunSuspiciousOperation();
    
    // Take snapshot and analyze
    var after = memoryProfiler.TakeSnapshot("after-leak", 1000);
    
    // Continue...
}
```

### Compare Two Specific Snapshots

1. Open both `.dmw` files in dotMemory
2. Use "Compare Snapshots" feature
3. Look for:
   - Types with increasing instance counts
   - Undisposed `IDisposable` objects
   - Large object heap (LOH) growth
   - String/byte array accumulation

## Common Memory Leak Patterns

### 1. Undisposed HttpResponseMessage
```csharp
// BAD
var response = await client.GetAsync(url);
// ... use response but never dispose

// GOOD
using var response = await client.GetAsync(url);
```

### 2. Event Handler Leaks
```csharp
// BAD
someObject.Event += Handler; // Never unsubscribed

// GOOD
someObject.Event += Handler;
// ... later ...
someObject.Event -= Handler;
```

### 3. Cache Bloat
```csharp
// Look for growing collections in snapshots
// Check HybridCache entry counts
// Verify eviction policies are working
```

### 4. ArrayPool Buffer Leaks
```csharp
// BAD
var buffer = ArrayPool<byte>.Shared.Rent(size);
// ... never returned

// GOOD
var buffer = ArrayPool<byte>.Shared.Rent(size);
try { /* use */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Run Memory Leak Detection
  run: |
    cd stress-tests/AppHost
    dotnet run &
    sleep 10  # Wait for startup
    
    cd ../Runner
    dotnet run -- memory-leak-suite
    
- name: Check for Leaks
  run: |
    $report = Get-Content ./profiles/*/analysis-report.json | ConvertFrom-Json
    if ($report.HasMemoryLeak) {
      Write-Error "Memory leak detected!"
      exit 1
    }
    
- name: Upload Memory Reports
  uses: actions/upload-artifact@v3
  with:
    name: memory-reports
    path: stress-tests/Runner/profiles/
```

## Troubleshooting

### "dotMemoryUnit not initialized"
- Ensure `JetBrains.dotMemoryUnit` package is installed
- Check that profiling is enabled in config

### Snapshots not being created
- Verify request count threshold (default: every 10K requests)
- Check permissions on `profiles/` directory
- Review console output for errors

### Large snapshot files
- Each snapshot can be 100-500MB
- Configure `MaxSnapshots` to limit retention
- Old snapshots are automatically deleted

### Missing dotMemory.exe
- Install JetBrains dotMemory on dev machine
- Or use dotMemoryUnit (in-process, no UI needed)

## Best Practices

1. **Run long-duration tests** (5+ minutes) to detect slow leaks
2. **Compare steady-state snapshots** (skip warmup period)
3. **Use mixed workloads** to simulate real usage patterns
4. **Monitor Gen2 collections** as early warning
5. **Automate in CI/CD** to catch regressions early

## Resources

- [JetBrains dotMemory Documentation](https://www.jetbrains.com/help/dotmemory/)
- [dotMemoryUnit Documentation](https://www.jetbrains.com/help/dotmemory-unit/)
- [.NET Memory Management](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [Finding Memory Leaks in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak)
