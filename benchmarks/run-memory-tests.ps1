# Quick Memory Allocation Test
# Runs a focused set of benchmarks to verify memory allocation patterns

Write-Host "Running Memory Allocation Benchmarks..." -ForegroundColor Cyan
Write-Host "This will take a few minutes. Results will show memory allocations and LOH usage." -ForegroundColor Yellow
Write-Host ""

# Run LOH benchmarks first (most critical for our architecture review)
Write-Host "=== LOH Benchmarks (Critical: Testing LOH threshold behavior) ===" -ForegroundColor Green
dotnet run -c Release --filter "*LOHBenchmarks*"

Write-Host ""
Write-Host "=== Memory Allocation Benchmarks (Testing various response sizes) ===" -ForegroundColor Green  
dotnet run -c Release --filter "*MemoryAllocationBenchmarks*"

Write-Host ""
Write-Host "=== Content Separation Benchmarks (Testing two-lookup overhead) ===" -ForegroundColor Green
dotnet run -c Release --filter "*ContentSeparationBenchmarks*"

Write-Host ""
Write-Host "Benchmark run complete! Check the results above." -ForegroundColor Cyan
Write-Host ""
Write-Host "Key things to look for:" -ForegroundColor Yellow
Write-Host "  1. Gen2 collections should be 0 for responses <85KB" -ForegroundColor White
Write-Host "  2. Gen2 >0 for responses >85KB is EXPECTED and acceptable" -ForegroundColor White
Write-Host "  3. Cache hits should have low allocation overhead" -ForegroundColor White
Write-Host "  4. Compression should help reduce LOH usage" -ForegroundColor White
