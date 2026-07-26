# Runs the http-tests/cache-tests RFC 9111 suite against HttpHybridCacheHandler
# via the ConformanceProxy, and gates the results against expected-results.json.
#
# Usage:
#   ./run-conformance.ps1              # run suite, compare against baseline
#   ./run-conformance.ps1 -Update      # run suite, rewrite the baseline
#   ./run-conformance.ps1 -TestId xyz  # run/debug a single test (no gating)
param(
    [switch]$Update,
    [string]$TestId,
    [int]$OriginPort = 8000,
    [int]$ProxyPort = 8081
)

$ErrorActionPreference = 'Stop'
$conformanceDir = $PSScriptRoot
$suiteDir = Join-Path $conformanceDir '.cache-tests'
$suiteRepo = 'https://github.com/http-tests/cache-tests.git'
$suitePin = 'b55b8bda3dbb8c927c04e85bd8d496a8caa3e4ba'
$proxyProject = Join-Path $conformanceDir 'ConformanceProxy\ConformanceProxy.csproj'
$resultsPath = Join-Path $conformanceDir 'results.json'
$baselinePath = Join-Path $conformanceDir 'expected-results.json'

function Wait-ForHttp([string]$Url, [int]$TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 2 -SkipHttpErrorCheck | Out-Null
            return
        } catch {
            Start-Sleep -Milliseconds 250
        }
    }
    throw "Timed out waiting for $Url"
}

# 1. Clone/update the suite at the pinned commit
if (-not (Test-Path (Join-Path $suiteDir '.git'))) {
    Write-Host "Cloning cache-tests into $suiteDir"
    git clone --quiet $suiteRepo $suiteDir
}
Push-Location $suiteDir
try {
    if ((git rev-parse HEAD) -ne $suitePin) {
        git fetch --quiet origin
        git checkout --quiet $suitePin
    }
    if (-not (Test-Path (Join-Path $suiteDir 'node_modules'))) {
        Write-Host 'Installing suite dependencies'
        npm install --no-audit --no-fund --silent
        if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
    }
} finally {
    Pop-Location
}

# 2. Build the proxy
dotnet build $proxyProject -v q --nologo
if ($LASTEXITCODE -ne 0) { throw 'Proxy build failed' }

$originProc = $null
$proxyProc = $null
try {
    # 3. Start the suite origin server and the caching proxy
    # Must launch via npm (server.mjs requires npm_package_config_* env vars).
    Write-Host "Starting suite server on :$OriginPort"
    $env:npm_config_port = "$OriginPort"
    try {
        $npmCmd = if ($IsWindows) { 'npm.cmd' } else { 'npm' }
        $originProc = Start-Process -FilePath $npmCmd `
            -ArgumentList 'run', 'server' `
            -WorkingDirectory $suiteDir -PassThru -WindowStyle Hidden
    } finally {
        Remove-Item Env:npm_config_port -ErrorAction SilentlyContinue
    }
    Write-Host "Starting ConformanceProxy on :$ProxyPort"
    $proxyProc = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run', '--project', $proxyProject, '--no-build', '--', `
            '--port', $ProxyPort, '--origin', "http://127.0.0.1:$OriginPort" `
        -WorkingDirectory $conformanceDir -PassThru -WindowStyle Hidden

    Wait-ForHttp "http://127.0.0.1:$OriginPort/"
    Wait-ForHttp "http://127.0.0.1:$ProxyPort/proxy-health"

    # 4. Run the suite client through the proxy
    Push-Location $suiteDir
    try {
        if ($TestId) {
            npm run cli "--base=http://127.0.0.1:$ProxyPort" "--id=$TestId"
            exit $LASTEXITCODE
        }
        Write-Host 'Running full suite (takes a few minutes)'
        npm run --silent cli "--base=http://127.0.0.1:$ProxyPort" > $resultsPath
        if ($LASTEXITCODE -ne 0) { throw 'Suite client failed' }
    } finally {
        Pop-Location
    }
} finally {
    foreach ($proc in @($proxyProc, $originProc)) {
        if ($proc -and -not $proc.HasExited) {
            if ($IsWindows) {
                # Kill the whole tree: npm.cmd/dotnet wrap the actual server process
                taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            } else {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# 5. Gate against (or update) the baseline
if ($Update) {
    node (Join-Path $conformanceDir 'compare-results.mjs') $resultsPath --update $baselinePath
} else {
    node (Join-Path $conformanceDir 'compare-results.mjs') $resultsPath $baselinePath
}
exit $LASTEXITCODE
