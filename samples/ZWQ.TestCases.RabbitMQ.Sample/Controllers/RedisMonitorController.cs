using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.Redis.Monitoring;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// Redis 心跳监控诊断接口
/// </summary>
[ApiController]
[Route("api/redis-monitor")]
public class RedisMonitorController : ControllerBase
{
    private readonly RedisHealthMonitor _healthMonitor;

    public RedisMonitorController(RedisHealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
    }

    /// <summary>
    /// 获取 Redis 健康状态总览
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _healthMonitor.GetStatus();
        return Ok(new
        {
            status.IsHealthy,
            status.IsConnected,
            status.Host,
            status.Port,
            pingStats = new
            {
                status.TotalPings,
                status.SuccessPings,
                status.FailedPings,
                status.ConsecutiveFailures,
                successRate = $"{status.SuccessRate:F1}%"
            },
            latency = new
            {
                lastMs = Math.Round(status.LastLatencyMs, 2),
                averageMs = Math.Round(status.AverageLatencyMs, 2)
            },
            status.LastSuccessTime,
            status.LastFailureTime
        });
    }

    /// <summary>
    /// 获取最近 N 条心跳记录
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] int count = 20)
    {
        var status = _healthMonitor.GetStatus();
        var records = status.RecentPings
            .OrderByDescending(r => r.Time)
            .Take(count)
            .Select(r => new
            {
                r.Time,
                r.Success,
                latencyMs = Math.Round(r.LatencyMs, 2),
                r.Error
            });

        return Ok(records);
    }

    /// <summary>
    /// 简易健康检查（适合负载均衡探针）
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        var status = _healthMonitor.GetStatus();

        if (status.IsHealthy)
            return Ok(new { status = "healthy", latencyMs = Math.Round(status.LastLatencyMs, 2) });

        return StatusCode(503, new
        {
            status = "unhealthy",
            consecutiveFailures = status.ConsecutiveFailures,
            lastFailureTime = status.LastFailureTime
        });
    }
}
