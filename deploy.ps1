$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

Write-Host ""
Write-Host "==================================================="
Write-Host "  ZWQ.TestCases - Docker Deploy"
Write-Host "==================================================="
Write-Host ""

# Switch to project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

# Check Docker
$null = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Docker is not running" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()

# ── Step 1: Build new image (old container still running, zero downtime) ──
Write-Host "[1/3] Building new image..." -ForegroundColor Cyan
$buildStart = Get-Date
docker-compose build app
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Build failed" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
$buildSec = [math]::Round(((Get-Date) - $buildStart).TotalSeconds)
Write-Host "      Build done in ${buildSec}s" -ForegroundColor DarkGray
Write-Host ""

# ── Step 2: Quick swap (only this step has ~2s downtime) ──
Write-Host "[2/3] Switching to new version..." -ForegroundColor Cyan
$swapStart = Get-Date
docker-compose up -d --force-recreate --remove-orphans app
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Failed to start" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
$swapSec = [math]::Round(((Get-Date) - $swapStart).TotalSeconds)
Write-Host "      Switched in ${swapSec}s" -ForegroundColor DarkGray
Write-Host ""

# ── Step 3: Clean up ──
Write-Host "[3/3] Cleaning old images..." -ForegroundColor Cyan
docker image prune -f 2>$null | Out-Null
Write-Host ""

# ── Health check ──
Write-Host "Waiting for app to start..." -ForegroundColor DarkGray
$ready = $false
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:8080/api/textsearch/stats" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) {
            $ready = $true
            break
        }
    } catch {
        Write-Host "  ... ($i s)" -ForegroundColor DarkGray
    }
}

$totalSec = [math]::Round($sw.Elapsed.TotalSeconds)
Write-Host ""

if ($ready) {
    Write-Host "[OK] Deploy done in ${totalSec}s (downtime ~${swapSec}s)" -ForegroundColor Green
} else {
    Write-Host "[WARN] App not ready yet, check logs: docker-compose logs -f app" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================================="
Write-Host "  Swagger:   http://localhost:8080"
Write-Host "  Qdrant:    http://localhost:6333/dashboard"
Write-Host ""
Write-Host "  Logs:      docker-compose logs -f app"
Write-Host "  Stop:      docker-compose down"
Write-Host "==================================================="
Write-Host ""
Read-Host "Press Enter to close"
