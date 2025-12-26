# Stress Tests

xUnit-based stress testing for HybridCacheHttpHandler with Redis L2 cache, Aspire orchestration, and JetBrains dotMemoryUnit integration.

## Status

✅ **Fully Functional** - All components build and run successfully

## Structure

- **AppHost/** - .NET Aspire orchestrator (Redis + Target) ✅
- **Target/** - Web API with test endpoints (various caching scenarios) ✅
- **StressTests/** - xUnit tests with dotMemoryUnit for memory profiling ✅
- **ServiceDefaults/** - Shared Aspire configuration ✅

## Quick Start

### Step 1: Start Infrastructure

```bash
# Terminal 1: Start Target API and Redis via Aspire
cd stress-tests/AppHost
dotnet run
```

**This starts:**
- Redis on localhost:6379
- Target API on http://localhost:5001
- Aspire Dashboard at https://apphost.dev.localhost:17233

### Step 2: Run Stress Tests

```bash
# Terminal 2: Run xUnit stress tests
cd stress-tests/StressTests
dotnet test
```

**Output:**
```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 2 s
✓ Cache Hit Ratio: 99.0%
✓ Memory Growth: 2.34 MB
```

## Test Suites

### 1. Cache Stampede Test (~2 seconds)
**Test:** `CacheStampede_ThunderingHerd_OnlyOneBackendCall`

- 100 concurrent requests to same endpoint
- Validates request collapsing (thundering herd protection)
- **Expected:** >95% cache hits, <10MB memory growth

### 2. Sustained Load Test (5 minutes, skipped by default)
**Test:** `SustainedLoad_5Minutes_NoMemoryLeak`

- 20 concurrent clients, ~400 req/s total
- Detects memory leaks under sustained load
- **Expected:** <1% error rate, <50MB memory growth

**Run explicitly:**
```bash
dotnet test --filter "SustainedLoad"
```

## Memory Profiling with dotMemoryUnit

### Automatic Snapshots

Tests decorated with `[DotMemoryUnit]` automatically:
- Capture memory snapshots at `dotMemory.Check()` calls
- Track allocations
- Analyze for memory leaks
- Generate .dmw snapshot files

### Viewing Snapshots

```bash
# Snapshot files location
cd stress-tests/StressTests/bin/Debug/net10.0/

# Open in dotMemory (if installed)
dotMemory.exe *.dmw
```

### Features
- ✅ Baseline and final snapshots
- ✅ Periodic snapshots every 10K requests
- ✅ Automatic leak detection
- ✅ Allocation hotspot analysis
- ✅ Object retention tracking

## Alternative: Manual Setup

```bash
# Terminal 1: Start Redis
docker run -d -p 6379:6379 redis:latest

# Terminal 2: Start Target web server
cd stress-tests/Target
dotnet run

# Terminal 3: Run tests
cd stress-tests/StressTests
$env:TARGET_URL="http://localhost:5001"
$env:ConnectionStrings__redis="localhost:6379"
dotnet test
```

## Target API Endpoints

Target provides these endpoints for testing:

### Cacheable Endpoints
- `GET /api/cacheable/{id}?size=1024&delay=0` - Configurable size and delay
- `GET /api/delay/{milliseconds}` - Artificial delay for stampede testing
- `GET /api/nocache` - No-cache directive
- `GET /api/nostore` - No-store directive  
- `GET /api/stale-while-revalidate` - RFC 5861 stale-while-revalidate
- `GET /api/stale-if-error?fail=false` - RFC 5861 stale-if-error

### Size Variants
- `GET /api/sizes/small` - 1KB response
- `GET /api/sizes/medium` - 100KB response
- `GET /api/sizes/large` - 5MB response

### Vary Header Testing
- `GET /api/vary/accept` - Vary by Accept header
- `GET /api/vary/encoding` - Vary by Accept-Encoding
- `GET /api/vary/language` - Vary by Accept-Language
- `GET /api/vary/multiple` - Vary by multiple headers

### Conditional Requests
- `GET /api/conditional/etag/{id}` - ETag support
- `GET /api/conditional/lastmodified/{id}` - Last-Modified support
- `GET /api/conditional/both/{id}` - Both ETag and Last-Modified

## Current Implementation

### ✅ Completed

**AppHost (Aspire Orchestrator):**
- Coordinates all services (Target, Runner, Redis)
- Aspire Dashboard integration
- Service discovery and configuration
- Redis container management

### Conditional Requests
- `GET /api/conditional/etag/{id}` - ETag support
- `GET /api/conditional/lastmodified/{id}` - Last-Modified support
- `GET /api/conditional/both/{id}` - Both ETag and Last-Modified

## Aspire Dashboard

When AppHost is running, access the dashboard to:
- **View Logs** - Real-time logs from Target and Redis
- **Monitor Metrics** - Performance counters
- **Trace Requests** - Distributed tracing (if OpenTelemetry enabled)
- **Check Resources** - Container and service status

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Stress Tests

on: [push, pull_request]

jobs:
  stress-test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Start Redis
      run: docker run -d -p 6379:6379 redis:latest
    
    - name: Start Target API
      run: |
        cd stress-tests/Target
        dotnet run &
        sleep 10
    
    - name: Run Stress Tests
      run: |
        cd stress-tests/StressTests
        dotnet test --logger "trx;LogFileName=test-results.trx"
    
    - name: Upload Test Results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results
        path: stress-tests/StressTests/TestResults/*.trx
```

## Troubleshooting

### "Target machine actively refused connection"
→ Start AppHost: `cd stress-tests/AppHost && dotnet run`

### "Value cannot be null (Parameter 'configuration')"
→ Set Redis connection string: `$env:ConnectionStrings__redis = "localhost:6379"`

### "No .dmw files generated"
→ Check: `stress-tests/StressTests/bin/Debug/net10.0/`  
→ Ensure dotMemoryUnit package is installed

### Tests timeout
→ Increase timeout:  
```bash
dotnet test -- RunConfiguration.TestSessionTimeout=600000
```

## Resources

- [dotMemoryUnit Documentation](https://www.jetbrains.com/help/dotmemory-unit/)
- [xUnit Documentation](https://xunit.net/)
- [Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [StressTests/README.md](StressTests/README.md) - Detailed test documentation

╭─────────────────────┬──────────────╮
│ Metric              │ Value        │
├─────────────────────┼──────────────┤
│ Duration            │ 1.23s        │
│ Total Requests      │ 100          │
│ Cache Hits          │ 99 (99.0%)   │
│ Latency P95         │ 110.00ms     │
│ Memory Peak         │ 47.80 MB     │
│ GC Gen2             │ 0            │
╰─────────────────────┴──────────────╯

Press any key to return to menu...
```

### Run All Suites Summary

```
╭───────────────────────────┬──────────┬──────────┬──────────┬──────────────┬────────────╮
│ Suite                     │ Status   │ Duration │ Requests │ Cache Hit %  │ P95 Latency│
├───────────────────────────┼──────────┼──────────┼──────────┼──────────────┼────────────┤
│ Cache Stampede Test       │ ✓ PASSED │ 1.23s    │ 100      │ 99.0%        │ 110.00ms   │
│ Mixed Workload Test       │ ✓ PASSED │ 15.67s   │ 1000     │ 85.3%        │ 245.50ms   │
│ Sustained Load Test       │ ✓ PASSED │ 300.12s  │ 50000    │ 92.1%        │ 189.23ms   │
╰───────────────────────────┴──────────┴──────────┴──────────┴──────────────┴────────────╯

Summary: 3 passed, 0 failed
```
