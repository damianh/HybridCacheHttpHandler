# HttpHybridCacheHandler Benchmarks

This directory contains benchmarks for analyzing the performance and memory allocation characteristics of the HttpHybridCacheHandler library.

## Measurement Scope

These are in-process L1 benchmarks with no network or distributed cache. The fake
origin shares preallocated, highly compressible byte payloads. Hit entries are
primed and verified in `GlobalSetup`. Requests use `ResponseHeadersRead` and copy
the body directly to `Stream.Null`, without caller-side buffering or string
conversion. Requests explicitly created by the benchmarks, responses, clients,
and service providers are disposed.

Miss benchmarks rebuild their cache in targeted `IterationSetup` and issue one
measured request per iteration. Cleanup verifies one origin call and a subsequent
cache hit. This bounds retained cache state rather than accumulating unique keys.
Do not override invocation count for these methods. Single-operation iterations
can produce noisy timing and allocation measurements; they are not steady-state
throughput tests.

Concurrent benchmarks dispatch each request with `Task.Run`. Their results include
thread-pool scheduling and task allocations, and are **per batch** (5 or 10 requests),
not per request. Hot hits may complete synchronously, so simply collecting async
calls in a loop would not exercise concurrent callers. These are not cold-cache
stampede tests.

The Vary benchmark measures retrieval of a primed variant with shared content,
not retained-memory savings from deduplication. No single-lookup control exists,
so these benchmarks do not isolate the cost of the second lookup.

`MemoryDiagnoser` reports managed allocations per operation and collections per
1,000 operations. It does not measure working set, peak/retained memory, native
allocations, or prove the absence of LOH objects when Gen2 is zero. Previous reports
using buffered reads or priming inside the measured method are not comparable.

## Running Benchmarks

### Run All Benchmarks
```bash
dotnet run -c Release -- --filter "*"
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*MemoryAllocationBenchmarks*"
dotnet run -c Release -- --filter "*ContentSeparationBenchmarks*"
dotnet run -c Release -- --filter "*LohBenchmarks*"
```

### Run Original Benchmarks
```bash
dotnet run -c Release -- --filter "*CachingBenchmarks*"
```

## Benchmark Categories

### StreamingFillBenchmarks

```bash
dotnet run -c Release -- --filter "*StreamingFillBenchmarks*"
```

Generates 1, 32, and 128 MiB responses without a preallocated body, requests headers-first,
and drains to `Stream.Null`. Runs with internal compression on and off. A discard-only
external store deliberately returns misses: this isolates handler staging and upload
streaming from SDK allocation/network costs and persistent body retention.

Compare allocation growth, throughput, and collections across sizes. Total allocations
can grow with the number of asynchronous reads even when live buffers remain bounded;
`MemoryDiagnoser` does not measure peak working set or spool-disk usage. These benchmarks
do not establish real-cloud interoperability or cloud-provider memory bounds.

### 1. MemoryAllocationBenchmarks
**Focus**: Memory allocation patterns across different response sizes

**Key Metrics to Watch**:
- `Allocated` - Total memory allocated per operation
- `Gen0` - Minor GC collections (young generation)
- `Gen1` - Intermediate GC collections
- `Gen2` - Full GC collections (LOH pressure can contribute)

**What We're Testing**:
- Cache miss (initial store) allocations for various sizes
- Cache hit (retrieval) allocations
- Batched concurrent hits
- Impact of response size on memory pressure

**Expected Behavior**:
- Small responses generally create less allocation pressure than large responses.
- Large contiguous allocations can reach the LOH.
- Compression reduces stored size but adds decompression allocations on hits.

### 2. ContentSeparationBenchmarks
**Focus**: Overhead and benefits of content/metadata separation

**Key Metrics to Watch**:
- End-to-end hit latency with two cache lookups (metadata + content)
- Memory allocations for small vs large responses
- Vary-variant retrieval with shared content

**What We're Testing**:
- Hit cost for different response sizes
- Retrieval of primed variants sharing content
- Concurrent access patterns with separated storage

**Expected Behavior**:
- Compare hit cost across response sizes; a separate control is needed to isolate lookup overhead.
- Deduplication savings require retained-memory measurements, not just allocations per hit.
- Concurrent hot hits do not exercise cold-cache stampede protection.

### 3. LohBenchmarks
**Focus**: Large Object Heap (LOH) behavior around the default 85,000-byte object threshold

**Key Metrics to Watch**:
- Gen2 collections (not a direct count of LOH allocations)
- Allocated memory for responses around LOH threshold
- Request-path allocation cost with and without compression

**What We're Testing**:
- 80 KiB (81,920 bytes): Payload below threshold
- 85 KiB (87,040 bytes): Already above threshold, not an exact boundary test
- 100 KiB+ (above threshold): Large contiguous payload arrays can reach LOH
- Compression: Stored bytes may shrink while decompressed output still reaches LOH

**Expected Behavior**:
Object headers, intermediate buffers, and pool bucket sizes also affect LOH
placement. Gen2 counts depend on GC activity over the run, not just payload size.

## Interpreting Results

### Memory Allocation Numbers

Illustrative output, not a measured baseline:

```
|                  Method |  Mean |     Allocated |  Gen0 | Gen1 | Gen2 |
|------------------------ |------:|--------------:|------:|-----:|-----:|
| SmallResponse_1KB       | 50us  |      5.2 KB   |  0.01 |    - |    - |
| LargeResponse_100KB     | 150us |    102.4 KB   |  0.05 |    - | 0.01 |
```

**Good Signs** ✅:
- Low `Allocated` values for cache hits
- Lower allocations for comparable hit workloads

**Expected Behavior** ⚠️:
- Large contiguous arrays can allocate on LOH.
- Compressed hits can allocate more than misses because of decompression.

**Concerning Signs** ❌:
- Excessive allocations on cache hits
- Regressions against the same workload on the same runtime and machine

### LOH Mitigation Strategies

If LOH becomes a problem (frequent Gen2 collections, memory fragmentation):

1. **Lower MaxCacheableContentSize**:
   ```csharp
   MaxCacheableContentSize = 80 * 1024 // Limit payload size, not all intermediate allocations
   ```

2. **Compression for Stored-Size Reduction**:
   ```csharp
   CompressionThreshold = 512 // Compress smaller responses
   ```
   This can increase request-path allocation pressure during decompression.

3. **Content Type Filtering**:
   ```csharp
   CacheableContentTypes = ["application/json", "text/*"] // Only cache compressible types
   ```

## Architecture Considerations

### Why LOH Usage is Acceptable

For SOA/distributed systems reliability:
- **Reliability > Performance**: Caching large responses reduces load on target systems
- **Infrequent Large Responses**: Most API calls are small (<10KB)
- **Gen2 Collection Cost**: Acceptable trade-off for system reliability
- **Compression Helps**: Text-based responses compress well

### Content/Metadata Separation Benefits

1. **Zero Base64 Overhead**: Content stored as raw bytes without Base64 expansion
2. **Content Deduplication**: Same content hash shared across cache entries
3. **Efficient 304 Updates**: Only metadata changes, content untouched

### Trade-offs Accepted

- ✅ Separate metadata and content lookups per hit
- ⚠️ LOH for large responses (acceptable for reliability goals)
- ✅ Compression trades CPU and temporary allocations for smaller stored content

## Baseline Expectations

Establish baselines from the current suite on the target runtime and machine.
There are no fixed allocation or Gen2 pass/fail thresholds. Compression trades
stored bytes for CPU and temporary allocations; it is not a guarantee of lower
request-path memory use.

## Continuous Monitoring

Run these benchmarks:
- Before major architectural changes
- When adding new caching features
- If production shows memory pressure issues
- To validate LOH mitigation strategies

## Contributing

When adding new benchmarks:
1. Use `[MemoryDiagnoser]` attribute
2. Document what you're testing and why
3. Include expected behavior in comments
4. Consider both memory and performance metrics
