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

# Step 1
Write-Host "[1/4] Stopping old containers..." -ForegroundColor Cyan
docker-compose down --remove-orphans 2>$null
Write-Host ""

# Step 2
Write-Host "[2/4] Building image (first time may take 2-3 min)..." -ForegroundColor Cyan
docker-compose build --no-cache app
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Build failed" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# Step 3
Write-Host "[3/4] Starting containers..." -ForegroundColor Cyan
docker-compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Failed to start" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# Step 4
Write-Host "[4/4] Cleaning old images..." -ForegroundColor Cyan
docker image prune -f 2>$null | Out-Null
Write-Host ""

# Wait
Write-Host "Waiting for app to start..."
Start-Sleep -Seconds 5

# Status
docker-compose ps
Write-Host ""

# Health check
try {
    $r = Invoke-WebRequest -Uri "http://localhost:8080/api/textsearch/stats" -UseBasicParsing -TimeoutSec 10
    if ($r.StatusCode -eq 200) {
        Write-Host "[OK] App is ready!" -ForegroundColor Green
    }
} catch {
    Write-Host "[WARN] App not ready yet, try refreshing browser in a few seconds" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================================="
Write-Host "  Deploy done!" -ForegroundColor Green
Write-Host ""
Write-Host "  Swagger:   http://localhost:8080"
Write-Host "  Qdrant:    http://localhost:6333/dashboard"
Write-Host ""
Write-Host "  Logs:      docker-compose logs -f app"
Write-Host "  Stop:      docker-compose down"
Write-Host "==================================================="
Write-Host ""
Read-Host "Press Enter to close"
