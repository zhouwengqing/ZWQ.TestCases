using System;
using System.Threading.Tasks;

namespace ZWQ.TestCases.Redis.Locking;

/// <summary>
/// 分布式锁接口
/// 基于 Redis SET NX EX 实现，支持 using 自动释放
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>锁是否获取成功</summary>
    bool IsAcquired { get; }
}

/// <summary>
/// Redis 分布式锁实现
/// </summary>
public class RedisDistributedLock : IDistributedLock
{
    private readonly StackExchange.Redis.IDatabase _db;
    private readonly string _key;
    private readonly string _lockValue;
    private bool _disposed;

    public bool IsAcquired { get; }

    internal RedisDistributedLock(StackExchange.Redis.IDatabase db, string key, string lockValue, bool acquired)
    {
        _db = db;
        _key = key;
        _lockValue = lockValue;
        IsAcquired = acquired;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || !IsAcquired) return;
        _disposed = true;

        // 仅释放自己持有的锁（比较值后删除）
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        await _db.ScriptEvaluateAsync(script,
            new StackExchange.Redis.RedisKey[] { _key },
            new StackExchange.Redis.RedisValue[] { _lockValue });
    }
}
