using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZWQ.TestCases.Redis.Connection;

namespace ZWQ.TestCases.Redis.Locking;

/// <summary>
/// 分布式锁服务接口
/// </summary>
public interface ILockService
{
    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiration">锁自动过期时间（防死锁）</param>
    /// <returns>锁实例，检查 IsAcquired 判断是否获取成功</returns>
    Task<IDistributedLock> TryAcquireAsync(string key, TimeSpan? expiration = null);

    /// <summary>
    /// 获取分布式锁，失败时等待重试
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiration">锁过期时间</param>
    /// <param name="waitTimeout">最大等待时间</param>
    /// <param name="retryInterval">重试间隔</param>
    Task<IDistributedLock> AcquireAsync(string key, TimeSpan? expiration = null,
        TimeSpan? waitTimeout = null, TimeSpan? retryInterval = null);
}

/// <summary>
/// 基于 Redis 的分布式锁服务
/// 使用 SET key value NX EX 原子命令，安全释放（Lua 脚本比较值后删除）
/// </summary>
public class RedisLockService : ILockService
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly ILogger<RedisLockService> _logger;

    public RedisLockService(RedisConnectionManager connectionManager, ILogger<RedisLockService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<IDistributedLock> TryAcquireAsync(string key, TimeSpan? expiration = null)
    {
        var lockKey = $"lock:{key}";
        var lockValue = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var expiry = expiration ?? TimeSpan.FromSeconds(30);

        var db = _connectionManager.GetDatabase();
        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

        if (acquired)
            _logger.LogDebug("[Redis Lock] 获取锁成功: {Key}", key);
        else
            _logger.LogDebug("[Redis Lock] 获取锁失败（已被持有）: {Key}", key);

        return new RedisDistributedLock(db, lockKey, lockValue, acquired);
    }

    public async Task<IDistributedLock> AcquireAsync(string key, TimeSpan? expiration = null,
        TimeSpan? waitTimeout = null, TimeSpan? retryInterval = null)
    {
        var timeout = waitTimeout ?? TimeSpan.FromSeconds(10);
        var interval = retryInterval ?? TimeSpan.FromMilliseconds(200);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var lockResult = await TryAcquireAsync(key, expiration);
            if (lockResult.IsAcquired)
                return lockResult;

            await Task.Delay(interval);
        }

        _logger.LogWarning("[Redis Lock] 等待超时，无法获取锁: {Key}", key);
        return new RedisDistributedLock(_connectionManager.GetDatabase(), $"lock:{key}", "", false);
    }
}
