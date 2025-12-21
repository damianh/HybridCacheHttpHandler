# HybridCacheHttpHandler Benchmarks

This directory contains benchmarks for analyzing the performance and memory allocation characteristics of the HybridCacheHttpHandler library.

## Running Benchmarks

### Run All Benchmarks
```bash
dotnet run -c Release
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release --filter "*MemoryAllocationBenchmarks*"
dotnet run -c Release --filter "*ContentSeparationBenchmarks*"
dotnet run -c Release --filter "*LOHBenchmarks*"
```

### Run Original Benchmarks
```bash
dotnet run -c Release --filter "*CachingBenchmarks*"
```

## Benchmark Categories

### 1. MemoryAllocationBenchmarks
**Focus**: Memory allocation patterns across different response sizes

**Key Metrics to Watch**:
- `Allocated` - Total memory allocated per operation
- `Gen0` - Minor GC collections (young generation)
- `Gen1` - Intermediate GC collections
- `Gen2` - Major GC collections (LOH allocations trigger Gen2)

**What We're Testing**:
- Cache miss (initial store) allocations for various sizes
- Cache hit (retrieval) allocations
- Content deduplication efficiency
- Impact of response size on memory pressure

**Expected Behavior**:
- Small responses (<85KB): Minimal Gen2 collections
- Large responses (>85KB): Gen2 collections indicate LOH usage (expected)
- Compression should reduce Gen2 collections for compressible content

### 2. ContentSeparationBenchmarks
**Focus**: Overhead and benefits of content/metadata separation

**Key Metrics to Watch**:
- Latency overhead of two cache lookups (metadata + content)
- Memory allocations for small vs large responses
- Content deduplication effectiveness

**What We're Testing**:
- Two-lookup penalty for different response sizes
- Whether content deduplication reduces memory footprint
- Concurrent access patterns with separated storage

**Expected Behavior**:
- Two-lookup overhead should be minimal for L1 (memory) cache
- Content deduplication should show memory savings when same content is cached with different Vary headers
- Concurrent requests should benefit from stampede protection

### 3. LOHBenchmarks
**Focus**: Large Object Heap (LOH) behavior around 85KB threshold

**Key Metrics to Watch**:
- Gen2 collections (indicates LOH usage)
- Allocated memory for responses around LOH threshold
- Effectiveness of compression in reducing LOH pressure

**What We're Testing**:
- 80KB (below threshold): Should avoid LOH
- 85KB (at threshold): Boundary condition
- 100KB+ (above threshold): LOH expected
- Compression effectiveness: Should reduce final size below LOH threshold for compressible content

**Expected Behavior**:
- **Below 85KB**: Gen2 = 0 (no LOH)
- **Above 85KB without compression**: Gen2 > 0 (LOH usage - **expected and acceptable**)
- **Above 85KB with compression**: Depends on compressibility
  - Highly compressible (text, JSON): May stay below LOH
  - Low compressibility (images): Will hit LOH (expected)

## Interpreting Results

### Memory Allocation Numbers

```
|                  Method |  Mean |     Allocated |  Gen0 | Gen1 | Gen2 |
|------------------------ |------:|--------------:|------:|-----:|-----:|
| SmallResponse_1KB       | 50us  |      5.2 KB   |  0.01 |    - |    - |
| LargeResponse_100KB     | 150us |    102.4 KB   |  0.05 |    - | 0.01 |
```

**Good Signs** ✅:
- Low `Allocated` values for cache hits
- Gen2 = 0 for responses <85KB
- Minimal overhead for two-lookup pattern

**Expected Behavior** ⚠️:
- Gen2 > 0 for responses >85KB (LOH - acceptable for reliability)
- Higher `Allocated` for cache misses (initial storage)

**Concerning Signs** ❌:
- Gen2 > 0 for small responses (<85KB)
- Excessive allocations on cache hits
- Significant overhead from two-lookup pattern

### LOH Mitigation Strategies

If LOH becomes a problem (frequent Gen2 collections, memory fragmentation):

1. **Lower MaxCacheableContentSize**:
   ```csharp
   MaxCacheableContentSize = 80 * 1024 // Stay below LOH
   ```

2. **Aggressive Compression**:
   ```csharp
   CompressionThreshold = 512 // Compress smaller responses
   ```

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

1. **Zero Base64 Overhead**: Content stored as raw bytes (33% savings)
2. **Content Deduplication**: Same content hash shared across cache entries
3. **Efficient 304 Updates**: Only metadata changes, content untouched

### Trade-offs Accepted

- ✅ Two cache lookups per request (minimal overhead with L1 cache)
- ⚠️ LOH for large responses (acceptable for reliability goals)
- ✅ Compression CPU cost (offsets storage and LOH concerns)

## Baseline Expectations

### Small Responses (1KB - 10KB)
- Cache Miss: ~10-20 KB allocated
- Cache Hit: ~2-5 KB allocated
- Gen2: 0

### Medium Responses (10KB - 85KB)
- Cache Miss: ~20-100 KB allocated
- Cache Hit: ~5-15 KB allocated
- Gen2: 0

### Large Responses (>85KB)
- Cache Miss: ~100KB+ allocated
- Cache Hit: ~10-30 KB allocated
- **Gen2: >0** (LOH usage - **expected**)
- Compression: May reduce to avoid LOH if highly compressible

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
