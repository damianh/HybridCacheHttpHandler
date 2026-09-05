# Quick Memory Allocation Test
# Runs a focused set of benchmarks to verify memory allocation patterns

Write-Host "Running Memory Allocation Benchmarks..." -ForegroundColor Cyan
Write-Host "This will take a few minutes. Results will show memory allocations and LOH usage." -ForegroundColor Yellow
Write-Host ""

# Run LOH benchmarks first (most critical for our architecture review)
Write-Host "=== LOH Benchmarks (Critical: Testing LOH threshold behavior) ===" -ForegroundColor Green
dotnet run -c Release -- --filter "*LohBenchmarks*"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "=== Memory Allocation Benchmarks (Testing various response sizes) ===" -ForegroundColor Green  
dotnet run -c Release -- --filter "*MemoryAllocationBenchmarks*"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "=== Content Separation Benchmarks (Testing two-lookup overhead) ===" -ForegroundColor Green
dotnet run -c Release -- --filter "*ContentSeparationBenchmarks*"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Benchmark run complete! Check the results above." -ForegroundColor Cyan
Write-Host ""
Write-Host "Key things to look for:" -ForegroundColor Yellow
Write-Host "  1. Gen2 counts do not directly measure LOH allocations" -ForegroundColor White
Write-Host "  2. Compare allocations only for equivalent workloads" -ForegroundColor White
Write-Host "  3. Concurrent results include scheduling and are per batch" -ForegroundColor White
Write-Host "  4. Compression saves storage but adds decompression allocations" -ForegroundColor White
