param(
    [switch]$Rollback
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

$ImageName = "mq-app"
$ContainerName = "zwq-testcases"
$MaxBackups = 3

Write-Host ""
if ($Rollback) {
    Write-Host "==================================================="
    Write-Host "  ZWQ.TestCases - Docker Rollback"
    Write-Host "==================================================="
} else {
    Write-Host "==================================================="
    Write-Host "  ZWQ.TestCases - Docker Deploy"
    Write-Host "==================================================="
}
Write-Host ""

# Check Docker
$null = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Docker is not running" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# ── Helper: list backup tags sorted by time (newest first) ──
function Get-BackupTags {
    docker images "$ImageName" --format "{{.Tag}}" 2>$null |
        Where-Object { $_ -match '^backup-\d{8}-\d{4}$' } |
        Sort-Object -Descending
}

# ── Helper: keep only $MaxBackups, remove older ones ──
function Remove-OldBackups {
    $tags = Get-BackupTags
    if ($tags.Count -gt $MaxBackups) {
        $toRemove = $tags[$MaxBackups..($tags.Count - 1)]
        foreach ($tag in $toRemove) {
            Write-Host "      Removing old backup: $tag" -ForegroundColor DarkGray
            docker rmi "${ImageName}:${tag}" 2>$null | Out-Null
        }
    }
}

# ══════════════════════════════════════════════════
#  ROLLBACK MODE
# ══════════════════════════════════════════════════
if ($Rollback) {
    $backups = Get-BackupTags
    if ($backups.Count -eq 0) {
        Write-Host "[ERROR] No backup found, cannot rollback" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }

    $latestBackup = $backups[0]
    Write-Host "Rolling back to: $latestBackup" -ForegroundColor Yellow
    Write-Host ""

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Host "[1/2] Stopping current container..." -ForegroundColor Cyan
    docker stop $ContainerName 2>$null | Out-Null
    docker rm $ContainerName 2>$null | Out-Null

    Write-Host "[2/2] Restoring from backup..." -ForegroundColor Cyan
    docker tag "${ImageName}:${latestBackup}" "${ImageName}:latest"
    docker-compose up -d app
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Rollback failed" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }

    Write-Host ""
    Write-Host "Waiting for app to start..." -ForegroundColor DarkGray
    $ready = $false
    for ($i = 1; $i -le 15; $i++) {
        Start-Sleep -Seconds 1
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:8080/api/textsearch/stats" -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        } catch {
            Write-Host "  ... ($i s)" -ForegroundColor DarkGray
        }
    }

    $totalSec = [math]::Round($sw.Elapsed.TotalSeconds)
    Write-Host ""
    if ($ready) {
        Write-Host "[OK] Rollback done in ${totalSec}s" -ForegroundColor Green
    } else {
        Write-Host "[WARN] App not ready, check logs: docker-compose logs -f app" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "  Available backups: $($backups -join ', ')" -ForegroundColor DarkGray
    Write-Host ""
    Read-Host "Press Enter to close"
    exit 0
}

# ══════════════════════════════════════════════════
#  DEPLOY MODE
# ══════════════════════════════════════════════════
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# ── Step 1: Backup current image ──
$currentImage = docker inspect "${ImageName}:latest" --format "{{.Id}}" 2>$null
if ($currentImage) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmm"
    $backupTag = "backup-$timestamp"
    Write-Host "[1/4] Backing up current image -> $backupTag" -ForegroundColor Cyan
    docker tag "${ImageName}:latest" "${ImageName}:${backupTag}"
    Remove-OldBackups
} else {
    Write-Host "[1/4] No existing image, skip backup" -ForegroundColor DarkGray
}
Write-Host ""

# ── Step 2: Build new image (old container still running) ──
Write-Host "[2/4] Building new image..." -ForegroundColor Cyan
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

# ── Step 3: Quick swap ──
Write-Host "[3/4] Switching to new version..." -ForegroundColor Cyan
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

# ── Step 4: Clean dangling images ──
Write-Host "[4/4] Cleaning dangling images..." -ForegroundColor Cyan
docker image prune -f 2>$null | Out-Null
Write-Host ""

# ── Health check ──
Write-Host "Waiting for app to start..." -ForegroundColor DarkGray
$ready = $false
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:8080/api/textsearch/stats" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
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

# Show available backups
$backups = Get-BackupTags
if ($backups.Count -gt 0) {
    Write-Host ""
    Write-Host "  Available backups: $($backups -join ', ')" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "==================================================="
Write-Host "  Swagger:   http://localhost:8080"
Write-Host "  Qdrant:    http://localhost:6333/dashboard"
Write-Host ""
Write-Host "  Rollback:  deploy.bat rollback"
Write-Host "  Logs:      docker-compose logs -f app"
Write-Host "  Stop:      docker-compose down"
Write-Host "==================================================="
Write-Host ""
Read-Host "Press Enter to close"
