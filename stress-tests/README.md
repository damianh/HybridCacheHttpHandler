# Stress Tests

Interactive stress testing for HybridCacheHttpHandler with Redis L2 cache and Aspire orchestration.

## Status

✅ **Fully Functional** - All components build and run successfully

## Structure

- **AppHost/** - .NET Aspire orchestrator (coordinates Target, Runner, Redis) ✅
- **Target/** - Web API with minimal endpoints (various caching scenarios) ✅
- **Runner/** - Interactive console test runner ✅

## Running with Aspire

### Recommended: Use Aspire AppHost

```bash
cd stress-tests/AppHost
dotnet run
```

This will:
- Start the Target web server on http://localhost:5001
- Start Redis container automatically
- Start the Runner console app with **interactive menu**
- Open Aspire Dashboard at http://localhost:15000

### Interactive Menu Features

The Runner provides a rich interactive menu with:

**Main Menu:**
- **Run Suite** - Select and run a specific test suite
- **Run All Suites** - Execute all suites sequentially
- **Configure** - Customize test parameters
- **View Configuration** - Display current settings
- **Exit** - Close the application

**Suite Execution:**
- Real-time progress bar with percentage
- Live metrics (cache hits/misses, average latency)
- Detailed results with color-coded status
- Summary statistics (latency percentiles, memory, GC)

**Configuration Options:**
- Concurrent Clients (1-1000)
- Total Requests (1-100,000)
- Duration (1-60 minutes)
- Target URL
- Enable/Disable Diagnostics
- Reset to Defaults

**Batch Execution:**
- Run all suites with summary table
- Pass/fail status for each suite
- Aggregate metrics across all tests

The Aspire Dashboard provides:
- **Logs** - View all application logs in real-time
- **Metrics** - Monitor performance metrics
- **Traces** - Distributed tracing across components
- **Resources** - Container and service status

### Alternative: Manual Setup

```bash
# Terminal 1: Start Target web server
cd stress-tests/Target
dotnet run

# Terminal 2: Start Redis
docker run -d -p 6379:6379 redis:latest

# Terminal 3: Run test suite
cd stress-tests/Runner
$env:TARGET_URL="http://localhost:5000"
$env:ConnectionStrings__redis="localhost:6379"
dotnet run
```

## Endpoints Available

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

**Target (Web API):**
- 15+ endpoints covering various caching scenarios
- Minimal API design
- Response generator (compressible, random, JSON content)
- Output caching configured

**Runner (Console App):**
- `InteractiveMenu` - Full Spectre.Console menu system ✅
- `CachedClientFactory` - Creates HttpClients with HybridCacheHttpHandler
- `MetricsCollector` - Latency (P50/P95/P99), memory, GC tracking  
- `ResultsPresenter` - Spectre.Console formatted output
- Configuration types (SuiteConfig, SuiteResult, SuiteMetrics)

**Test Suites:**
- `CacheStampedeSuite` - 100 concurrent requests testing request collapsing

**Integration:**
- Added to HybridCacheHttpHandler.slnx
- All projects build successfully ✅
- Redis L2 cache configured via StackExchangeRedis
- Aspire service discovery integrated

### 📋 TODO

**High Priority:**
1. **More Test Suites:**
   - `MixedWorkloadSuite` - Various endpoints and sizes
   - `SustainedLoadSuite` - Long-running stability (5+ minutes)
   - `VaryHeaderSuite` - Content negotiation caching
   - `ContentTypeSuite` - Compression behavior validation
   - `ConditionalRequestSuite` - ETag/Last-Modified under load
   - `L2CacheSuite` - Redis distributed cache validation

**Medium Priority:**
2. **Enhanced Reporting:**
   - Export results to JSON/CSV
   - Comparison mode (before/after)
   - Historical trending
3. **Configurable Parameters:**
   - appsettings.json for suite customization
   - Command-line arguments

## Example Output

### Main Menu

```
══════════════════════════════════════════════════════
     HybridCacheHttpHandler Stress Test Runner
══════════════════════════════════════════════════════

? What would you like to do?
  > Run Suite
    Run All Suites
    Configure
    View Configuration
    Exit
```

### Suite Execution
### Suite Execution

```
══════════════════════════════════════════════════════
     HybridCacheHttpHandler Stress Test Runner
══════════════════════════════════════════════════════

Running Cache Stampede Test... ████████████████░░░░ 82% 
(Hits: 81, Misses: 1, Avg: 105.23ms)
```

### Test Results

```
══════════════════════════════════════════════════════
     HybridCacheHttpHandler Stress Test Runner
══════════════════════════════════════════════════════

╔══════════════════════════════════════════╗
║  Cache Stampede Test - PASSED            ║
╚══════════════════════════════════════════╝

Summary:
✓ Request collapsing worked - only 1-2 backend calls

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
