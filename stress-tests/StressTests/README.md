# Stress Tests (xUnit + dotMemoryUnit)

xUnit-based stress tests with integrated memory profiling via JetBrains dotMemoryUnit.

## Why xUnit Instead of Console App?

**Previous Approach:** Console app with manual dotMemoryUnit calls → didn't work properly

**New Approach:** xUnit tests with `[DotMemoryUnit]` attributes → works perfectly

### Benefits
- ✅ dotMemoryUnit works properly with test framework
- ✅ Automatic .dmw snapshot generation
- ✅ `dotnet test` integration
- ✅ Test isolation per suite
- ✅ CI/CD friendly
- ✅ IDE test explorer support (Rider, VS)
- ✅ Can run specific tests with `--filter`

## Running Stress Tests

### Step 1: Start Infrastructure

```bash
# Terminal 1: Start Target + Redis via Aspire
cd stress-tests/AppHost
dotnet run
```

**This starts:**
- Redis on localhost:6379
- Target API on http://localhost:5001
- Aspire Dashboard at https://apphost.dev.localhost:17233

### Step 2: Run Tests

```bash
# Terminal 2: Run stress tests
cd stress-tests/StressTests
dotnet test
```

**Output:**
```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 2 s
```

### Run Specific Test

```bash
# Run only the cache stampede test
dotnet test --filter "FullyQualifiedName~CacheStampede"

# Run the long-running sustained load test (5 minutes)
dotnet test --filter "FullyQualifiedName~SustainedLoad"
```

### Run with Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Suites

### 1. CacheStampede_ThunderingHerd_OnlyOneBackendCall

**Duration:** ~2 seconds  
**Load:** 100 concurrent requests to same endpoint  
**Purpose:** Validate request collapsing (thundering herd protection)

**dotMemoryUnit Features:**
- Baseline snapshot before test
- Final snapshot after test
- Automatic leak detection
- Allocation tracking

**Expected Results:**
- ✅ Cache hit ratio > 95%
- ✅ Memory growth < 10MB
- ✅ Only 1-2 backend calls

### 2. SustainedLoad_5Minutes_NoMemoryLeak

**Duration:** 5 minutes  
**Load:** 20 concurrent clients, ~400 req/s total  
**Purpose:** Detect memory leaks under sustained load

**dotMemoryUnit Features:**
- Baseline snapshot
- Periodic snapshots every 10K requests
- Final snapshot
- Leak analysis across snapshots

**Expected Results:**
- ✅ Error rate < 1%
- ✅ Memory growth < 50MB
- ✅ No linear memory growth pattern

**Note:** Skipped by default due to duration. Run explicitly:
```bash
dotnet test --filter "SustainedLoad"
```

## dotMemoryUnit Integration

### Automatic Memory Profiling

Each test decorated with `[DotMemoryUnit]` automatically:
1. Captures memory snapshots at `dotMemory.Check()` calls
2. Tracks allocations (if `CollectAllocations = true`)
3. Analyzes for memory leaks
4. Generates .dmw snapshot files

### Snapshot Files

Located in: `stress-tests/StressTests/bin/Debug/net10.0/`

Files:
- `*.dmw` - dotMemory workspace files
- Can be opened in dotMemory UI for deep analysis

### Viewing Snapshots

```bash
# Open in dotMemory (if installed)
dotMemory.exe stress-tests/StressTests/bin/Debug/net10.0/*.dmw
```

Or use dotMemory's comparison features to analyze:
- Object retention
- Allocation hotspots
- GC survivors
- Memory traffic

## Configuration

### Environment Variables

```bash
# Target API URL (default: http://localhost:5001)
$env:TARGET_URL = "http://localhost:5001"

# Redis connection string (default: localhost:6379)
$env:ConnectionStrings__redis = "localhost:6379"
```

### Test Attributes

```csharp
[Fact]
[DotMemoryUnit(
    CollectAllocations = true,      // Track all allocations
    FailIfRunWithoutSupport = false // Don't fail if dotMemoryUnit unavailable
)]
public async Task MyStressTest()
{
    // Baseline
    dotMemory.Check(memory => { });
    
    // ... run test ...
    
    // Final
    dotMemory.Check(memory => { });
}
```

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

## Comparison: Console Runner vs xUnit

| Feature | Console Runner | xUnit Tests |
|---------|---------------|-------------|
| dotMemoryUnit | ❌ Doesn't work | ✅ Works perfectly |
| .dmw Snapshots | ❌ Not generated | ✅ Auto-generated |
| Test Isolation | ❌ Sequential only | ✅ Per test |
| Selective Runs | ❌ All or nothing | ✅ --filter support |
| IDE Integration | ❌ None | ✅ Test Explorer |
| CI/CD | ⚠️ Complex | ✅ Standard |
| Aspire Logs | ✅ ILogger | ❌ Test output only |

**Recommendation:** Use xUnit for stress tests, especially with dotMemoryUnit.

## Troubleshooting

### "Target machine actively refused connection"
→ Start AppHost: `cd stress-tests/AppHost && dotnet run`

### "Value cannot be null (Parameter 'configuration')"
→ Set Redis connection string: `$env:ConnectionStrings__redis = "localhost:6379"`

### "No .dmw files generated"
→ Check test output directory: `stress-tests/StressTests/bin/Debug/net10.0/`  
→ Ensure dotMemoryUnit package is installed

### Tests timeout
→ Increase test timeout in .runsettings or via CLI:  
```bash
dotnet test -- RunConfiguration.TestSessionTimeout=600000
```

## Manual Testing Workflow

```bash
# 1. Start infrastructure
cd stress-tests/AppHost
dotnet run

# Wait for "Now listening on..." message

# 2. In new terminal, run quick test
cd stress-tests/StressTests
dotnet test --filter "CacheStampede"

# 3. Check results
echo "Test passed! Check bin/Debug/net10.0/ for .dmw files"

# 4. Run long test (optional)
dotnet test --filter "SustainedLoad"

# 5. Analyze memory snapshots in dotMemory UI
dotMemory.exe bin/Debug/net10.0/*.dmw
```

## Tips

- **Quick validation:** Run `CacheStampede` test (~2 seconds)
- **Memory leak hunting:** Run `SustainedLoad` test (5 minutes)
- **Compare snapshots:** Open multiple .dmw files in dotMemory to compare
- **Check Aspire Dashboard:** View logs/traces while tests run
- **Parallel execution:** xUnit runs tests in parallel by default (can disable with `[Collection]`)

## Resources

- [dotMemoryUnit Documentation](https://www.jetbrains.com/help/dotmemory-unit/)
- [xUnit Documentation](https://xunit.net/)
- [Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
