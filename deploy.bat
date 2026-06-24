@echo off
chcp 65001 >nul 2>&1
title ZWQ.TestCases Docker 一键部署

echo.
echo ═══════════════════════════════════════════════════
echo   ZWQ.TestCases — Docker 一键发布更新
echo ═══════════════════════════════════════════════════
echo.

:: 检查 Docker 是否运行
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] Docker 未运行，请先启动 Docker Desktop
    pause
    exit /b 1
)

:: 切换到脚本所在目录（即项目根目录）
cd /d "%~dp0"

echo [1/4] 停止旧容器...
docker-compose down --remove-orphans 2>nul
echo.

echo [2/4] 构建新镜像（增量编译，首次较慢后续很快）...
docker-compose build --no-cache app
if %errorlevel% neq 0 (
    echo.
    echo [错误] 镜像构建失败，请检查编译错误
    pause
    exit /b 1
)
echo.

echo [3/4] 启动容器...
docker-compose up -d
if %errorlevel% neq 0 (
    echo.
    echo [错误] 容器启动失败
    pause
    exit /b 1
)
echo.

echo [4/4] 清理旧镜像...
docker image prune -f >nul 2>&1
echo.

:: 等待 app 就绪
echo 等待应用启动...
timeout /t 5 /nobreak >nul

:: 检查容器状态
docker-compose ps
echo.

:: 健康检查
powershell -Command "try { $r = Invoke-WebRequest -Uri 'http://localhost:8080/api/textsearch/stats' -UseBasicParsing -TimeoutSec 10; if ($r.StatusCode -eq 200) { Write-Host '[OK] 应用已就绪' -ForegroundColor Green } else { Write-Host '[WARN] 应用返回状态码:' $r.StatusCode -ForegroundColor Yellow } } catch { Write-Host '[WARN] 应用尚未完全启动，请稍后刷新浏览器' -ForegroundColor Yellow }"

echo.
echo ═══════════════════════════════════════════════════
echo   发布完成！
echo.
echo   Swagger:      http://localhost:8080
echo   Qdrant:       http://localhost:6333/dashboard
echo.
echo   查看日志:     docker-compose logs -f app
echo   停止服务:     docker-compose down
echo ═══════════════════════════════════════════════════
echo.
pause
