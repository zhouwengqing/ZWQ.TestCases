using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.Redis.Caching;
using ZWQ.TestCases.Redis.Connection;
using ZWQ.TestCases.Redis.Locking;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// Redis 缓存测试接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RedisController : ControllerBase
{
    private readonly ICacheService _cache;
    private readonly ILockService _lockService;
    private readonly RedisConnectionManager _connectionManager;

    public RedisController(ICacheService cache, ILockService lockService, RedisConnectionManager connectionManager)
    {
        _cache = cache;
        _lockService = lockService;
        _connectionManager = connectionManager;
    }

    // ====== 连接状态 ======

    /// <summary>
    /// 检查 Redis 连接状态
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        try
        {
            var db = _connectionManager.GetDatabase();
            var result = db.Ping();
            return Ok(new
            {
                connected = _connectionManager.IsConnected,
                pingMs = result.TotalMilliseconds,
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return Ok(new { connected = false, error = ex.Message });
        }
    }

    // ====== 基础缓存操作 ======

    /// <summary>
    /// 设置缓存
    /// </summary>
    [HttpPost("set")]
    public async Task<IActionResult> SetCache([FromBody] SetCacheRequest request)
    {
        var expiration = request.ExpirationSeconds.HasValue
            ? TimeSpan.FromSeconds(request.ExpirationSeconds.Value)
            : (TimeSpan?)null;

        if (request.Value is not null)
            await _cache.SetStringAsync(request.Key, request.Value, expiration);
        else if (request.ObjectValue is not null)
            await _cache.SetAsync(request.Key, request.ObjectValue, expiration);

        return Ok(new { message = $"缓存已设置: {request.Key}" });
    }

    /// <summary>
    /// 获取缓存
    /// </summary>
    [HttpGet("get/{key}")]
    public async Task<IActionResult> GetCache(string key)
    {
        var value = await _cache.GetStringAsync(key);
        if (value == null)
            return NotFound(new { message = $"缓存未找到: {key}" });

        return Ok(new { key, value, exists = true });
    }

    /// <summary>
    /// 删除缓存
    /// </summary>
    [HttpDelete("delete/{key}")]
    public async Task<IActionResult> DeleteCache(string key)
    {
        var result = await _cache.RemoveAsync(key);
        return Ok(new { key, deleted = result });
    }

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    [HttpGet("exists/{key}")]
    public async Task<IActionResult> Exists(string key)
    {
        var exists = await _cache.ExistsAsync(key);
        return Ok(new { key, exists });
    }

    // ====== 高级操作 ======

    /// <summary>
    /// GetOrSet 测试 — 不存在时自动创建
    /// </summary>
    [HttpGet("getorset/{key}")]
    public async Task<IActionResult> GetOrSet(string key)
    {
        var result = await _cache.GetOrSetAsync(key, async () =>
        {
            // 模拟从数据库加载数据
            await Task.Delay(500);
            return new
            {
                source = "database",
                data = $"模拟数据 - 生成于 {DateTime.Now:HH:mm:ss}",
                key
            };
        }, TimeSpan.FromMinutes(5));

        return Ok(result);
    }

    /// <summary>
    /// 原子计数器测试
    /// </summary>
    [HttpPost("counter/{key}")]
    public async Task<IActionResult> Counter(string key, [FromQuery] string action = "increment", [FromQuery] long value = 1)
    {
        long result;
        if (action == "decrement")
            result = await _cache.DecrementAsync(key, value);
        else
            result = await _cache.IncrementAsync(key, value);

        return Ok(new { key, action, value, result });
    }

    // ====== 分布式锁测试 ======

    /// <summary>
    /// 测试分布式锁
    /// </summary>
    [HttpPost("lock/{key}")]
    public async Task<IActionResult> TestLock(string key, [FromQuery] int holdSeconds = 5)
    {
        await using var lockResult = await _lockService.TryAcquireAsync(key, TimeSpan.FromSeconds(30));

        if (!lockResult.IsAcquired)
            return Conflict(new { message = $"锁 '{key}' 已被其他进程持有" });

        // 模拟持锁期间的业务操作
        await Task.Delay(TimeSpan.FromSeconds(Math.Min(holdSeconds, 10)));

        return Ok(new
        {
            message = $"锁 '{key}' 获取成功，持有 {holdSeconds} 秒后自动释放",
            lockKey = key,
            heldSeconds = holdSeconds
        });
    }

    /// <summary>
    /// 等待式分布式锁测试（会等待重试）
    /// </summary>
    [HttpPost("lock-wait/{key}")]
    public async Task<IActionResult> TestLockWithWait(string key, [FromQuery] int waitSeconds = 10)
    {
        await using var lockResult = await _lockService.AcquireAsync(
            key,
            expiration: TimeSpan.FromSeconds(30),
            waitTimeout: TimeSpan.FromSeconds(waitSeconds));

        if (!lockResult.IsAcquired)
            return Conflict(new { message = $"等待 {waitSeconds} 秒后仍无法获取锁 '{key}'" });

        return Ok(new { message = $"锁 '{key}' 获取成功（可能经历了等待）", lockKey = key });
    }
}

// ====== DTO ======

public class SetCacheRequest
{
    public string Key { get; set; } = "test_key";
    public string? Value { get; set; }
    public object? ObjectValue { get; set; }
    public int? ExpirationSeconds { get; set; }
}
