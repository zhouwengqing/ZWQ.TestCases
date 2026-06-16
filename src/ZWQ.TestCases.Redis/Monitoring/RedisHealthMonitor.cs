using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.Redis.Connection;
using ZWQ.TestCases.Redis.Options;
using Microsoft.Extensions.Options;

namespace ZWQ.TestCases.Redis.Monitoring;

/// <summary>
/// Redis 心跳监控（BackgroundService）
/// 
/// 功能：
///   1. 定时 PING Redis，检测连接可用性
///   2. 记录 PING 延迟、成功/失败次数
///   3. 检测连接状态变化（断线 → 恢复 / 正常 → 断线），触发告警日志
///   4. 暴露 HealthStatus 供诊断接口查询
/// </summary>
public class RedisHealthMonitor : BackgroundService
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisHealthMonitor> _logger;

    // ====== 健康状态（线程安全） ======
    private readonly ConcurrentQueue<PingRecord> _recentPings = new();
    private readonly int _maxRecentRecords = 100;
    private long _totalPings;
    private long _successPings;
    private long _failedPings;
    private long _consecutiveFailures;
    private DateTime _lastSuccessTime;
    private DateTime _lastFailureTime;
    private double _lastLatencyMs;
    private bool _wasHealthy = true;

    public RedisHealthMonitor(
        RedisConnectionManager connectionManager,
        IOptions<RedisOptions> options,
        ILogger<RedisHealthMonitor> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
        _lastSuccessTime = DateTime.MinValue;
        _lastFailureTime = DateTime.MinValue;
    }

    /// <summary>心跳间隔（秒），默认 15 秒</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 15;

    /// <summary>获取当前健康状态快照</summary>
    public RedisHealthStatus GetStatus()
    {
        var isConnected = _connectionManager.IsConnected;
        var avgLatency = CalculateAverageLatency();

        return new RedisHealthStatus
        {
            IsHealthy = isConnected && _consecutiveFailures == 0,
            IsConnected = isConnected,
            TotalPings = Interlocked.Read(ref _totalPings),
            SuccessPings = Interlocked.Read(ref _successPings),
            FailedPings = Interlocked.Read(ref _failedPings),
            ConsecutiveFailures = Interlocked.Read(ref _consecutiveFailures),
            LastLatencyMs = _lastLatencyMs,
            AverageLatencyMs = avgLatency,
            LastSuccessTime = _lastSuccessTime,
            LastFailureTime = _lastFailureTime,
            Host = _options.Host,
            Port = _options.Port,
            RecentPings = _recentPings.ToArray()
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Redis Monitor] 心跳监控已启动，间隔 {Interval} 秒", HeartbeatIntervalSeconds);

        // 等待 5 秒让应用初始化完成
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoPingAsync();

            try { await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("[Redis Monitor] 心跳监控已停止");
    }

    private async Task DoPingAsync()
    {
        Interlocked.Increment(ref _totalPings);

        try
        {
            var db = _connectionManager.GetDatabase();
            var latency = await db.PingAsync();
            var latencyMs = latency.TotalMilliseconds;

            _lastLatencyMs = latencyMs;
            _lastSuccessTime = DateTime.Now;
            Interlocked.Increment(ref _successPings);
            Interlocked.Exchange(ref _consecutiveFailures, 0);

            // 记录
            AddRecentPing(true, latencyMs);

            // 状态变化检测：从断线恢复
            if (!_wasHealthy)
            {
                _wasHealthy = true;
                _logger.LogInformation("[Redis Monitor] ✅ 连接已恢复！最后成功时间: {Time}", DateTime.Now);
            }

            // 延迟过高告警
            if (latencyMs > 100)
                _logger.LogWarning("[Redis Monitor] ⚠️ PING 延迟过高: {Latency:F2}ms", latencyMs);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedPings);
            Interlocked.Increment(ref _consecutiveFailures);
            _lastFailureTime = DateTime.Now;

            AddRecentPing(false, 0, ex.Message);

            // 状态变化检测：从正常变为断线
            if (_wasHealthy)
            {
                _wasHealthy = false;
                _logger.LogError(ex, "[Redis Monitor] ❌ 连接断开！时间: {Time}", DateTime.Now);
            }
            else
            {
                var failures = Interlocked.Read(ref _consecutiveFailures);
                // 每 10 次连续失败才打一次警告，避免日志轰炸
                if (failures % 10 == 0)
                    _logger.LogWarning("[Redis Monitor] 连续 {Count} 次心跳失败", failures);
            }
        }
    }

    private void AddRecentPing(bool success, double latencyMs, string? error = null)
    {
        var record = new PingRecord
        {
            Time = DateTime.Now,
            Success = success,
            LatencyMs = latencyMs,
            Error = error
        };

        _recentPings.Enqueue(record);

        // 保留最近 N 条
        while (_recentPings.Count > _maxRecentRecords)
            _recentPings.TryDequeue(out _);
    }

    private double CalculateAverageLatency()
    {
        var records = _recentPings.ToArray();
        var successRecords = 0;
        double totalMs = 0;
        foreach (var r in records)
        {
            if (r.Success)
            {
                successRecords++;
                totalMs += r.LatencyMs;
            }
        }
        return successRecords > 0 ? totalMs / successRecords : 0;
    }
}

// ====== 数据模型 ======

/// <summary>
/// Redis 健康状态快照
/// </summary>
public class RedisHealthStatus
{
    public bool IsHealthy { get; set; }
    public bool IsConnected { get; set; }
    public long TotalPings { get; set; }
    public long SuccessPings { get; set; }
    public long FailedPings { get; set; }
    public long ConsecutiveFailures { get; set; }
    public double LastLatencyMs { get; set; }
    public double AverageLatencyMs { get; set; }
    public DateTime LastSuccessTime { get; set; }
    public DateTime LastFailureTime { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public PingRecord[] RecentPings { get; set; } = Array.Empty<PingRecord>();

    /// <summary>成功率百分比</summary>
    public double SuccessRate => TotalPings > 0 ? (double)SuccessPings / TotalPings * 100 : 0;
}

/// <summary>
/// 单次 PING 记录
/// </summary>
public class PingRecord
{
    public DateTime Time { get; set; }
    public bool Success { get; set; }
    public double LatencyMs { get; set; }
    public string? Error { get; set; }
}
