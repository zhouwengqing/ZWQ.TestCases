using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZWQ.TestCases.Redis.Connection;
using ZWQ.TestCases.Redis.Options;

namespace ZWQ.TestCases.Redis.Caching;

/// <summary>
/// 基于 StackExchange.Redis 的分布式缓存实现
/// 
/// 内置三重防护:
///   1. 穿透防护 — 缓存空值（工厂返回 null 时写入占位记录，短 TTL）
///   2. 击穿防护 — GetOrSet 加分布式互斥锁（SET NX），仅一个线程回源
///   3. 雪崩防护 — TTL 随机抖动（±N%），避免大批 key 同时过期
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    /// <summary>空值占位标记（穿透防护）</summary>
    private const string NullSentinel = "__NULL__";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [ThreadStatic]
    private static Random? _random;
    private static Random LocalRandom => _random ??= new Random();

    public RedisCacheService(
        RedisConnectionManager connectionManager,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    private IDatabase Db => _connectionManager.GetDatabase();
    private string PrefixKey(string key) => string.IsNullOrEmpty(_options.KeyPrefix) ? key : $"{_options.KeyPrefix}:{key}";
    private TimeSpan DefaultExpiration => TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);

    // ====== 雪崩防护：TTL 随机抖动 ======

    /// <summary>
    /// 给过期时间加上随机抖动，避免大批 key 同时过期
    /// 例如 jitter=10% 且 ttl=30min → 实际范围 27~33 min
    /// </summary>
    private TimeSpan ApplyJitter(TimeSpan ttl)
    {
        if (_options.ExpirationJitterPercent <= 0) return ttl;

        var jitterRatio = _options.ExpirationJitterPercent / 100.0;
        var minMs = ttl.TotalMilliseconds * (1 - jitterRatio);
        var maxMs = ttl.TotalMilliseconds * (1 + jitterRatio);
        var jitteredMs = minMs + LocalRandom.NextDouble() * (maxMs - minMs);
        return TimeSpan.FromMilliseconds(jitteredMs);
    }

    // ====== 基础操作 ======

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await Db.StringGetAsync(PrefixKey(key));
            if (value.IsNullOrEmpty) return null;

            // 穿透防护：识别空值占位标记
            var str = (string)value!;
            if (str == NullSentinel)
            {
                _logger.LogDebug("[Redis] 命中空值占位，直接返回 null: {Key}", key);
                return null;
            }

            return JsonSerializer.Deserialize<T>(str, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Redis] GetAsync 失败: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var expiry = ApplyJitter(expiration ?? DefaultExpiration);

        await Db.StringSetAsync(PrefixKey(key), json, expiry);
    }

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await Db.StringGetAsync(PrefixKey(key));
        if (value.IsNullOrEmpty) return null;

        var str = value.ToString();
        return str == NullSentinel ? null : str;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
    {
        var expiry = ApplyJitter(expiration ?? DefaultExpiration);
        await Db.StringSetAsync(PrefixKey(key), value, expiry);
    }

    // ====== 删除与检查 ======

    public async Task<bool> RemoveAsync(string key)
    {
        return await Db.KeyDeleteAsync(PrefixKey(key));
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await Db.KeyExistsAsync(PrefixKey(key));
    }

    // ====== 高级操作（含穿透 + 击穿防护） ======

    /// <summary>
    /// 获取缓存，不存在则通过工厂方法加载并缓存。
    /// 
    /// 穿透防护：工厂返回 null 时缓存占位标记（短 TTL），后续请求不再查库
    /// 击穿防护：缓存未命中时先抢分布式锁（SET NX），仅一个线程执行工厂回源；
    ///           未抢到锁的线程短暂等待后重新读取缓存
    /// 雪崩防护：写入缓存时 TTL 自动加上随机抖动
    /// </summary>
    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class
    {
        // 第一次读缓存（含穿透识别）
        var cached = await GetAsync<T>(key);
        if (cached != null) return cached;

        // 检查是否是空值占位（穿透防护 — 已缓存的不存在数据）
        var rawValue = await Db.StringGetAsync(PrefixKey(key));
        if (!rawValue.IsNullOrEmpty && (string)rawValue! == NullSentinel)
            return null;

        // ====== 击穿防护：抢分布式锁 ======
        if (_options.EnableBreakdownLock)
        {
            var lockKey = PrefixKey($"lock:cache:{key}");
            var lockValue = $"{Environment.MachineName}:{Guid.NewGuid():N}";
            var lockExpiry = TimeSpan.FromSeconds(10); // 防死锁

            var acquired = await Db.StringSetAsync(lockKey, lockValue, lockExpiry, When.NotExists);

            if (acquired)
            {
                try
                {
                    // 双重检查（DCL）— 可能在等锁期间被其他线程已写入
                    cached = await GetAsync<T>(key);
                    if (cached != null) return cached;

                    // 执行工厂方法回源
                    var value = await factory();

                    if (value != null)
                    {
                        // 正常缓存 + 雪崩防护（TTL 抖动）
                        await SetAsync(key, value, expiration);
                    }
                    else if (_options.CacheNullValues)
                    {
                        // 穿透防护：缓存空值占位
                        var nullExpiry = TimeSpan.FromMinutes(_options.NullValueExpirationMinutes);
                        await Db.StringSetAsync(PrefixKey(key), NullSentinel, nullExpiry);
                        _logger.LogDebug("[Redis] 穿透防护：已缓存空值占位 {Key}，{Minutes}分钟后过期",
                            key, _options.NullValueExpirationMinutes);
                    }

                    return value;
                }
                finally
                {
                    // Lua 脚本安全释放锁（仅释放自己持有的）
                    var script = @"
                        if redis.call('get', KEYS[1]) == ARGV[1] then
                            return redis.call('del', KEYS[1])
                        else
                            return 0
                        end";
                    await Db.ScriptEvaluateAsync(script,
                        new RedisKey[] { lockKey },
                        new RedisValue[] { lockValue });
                }
            }
            else
            {
                // 未抢到锁 → 等待持有锁的线程写入缓存后重读
                _logger.LogDebug("[Redis] 击穿防护：等待其他线程加载缓存 {Key}", key);
                await Task.Delay(TimeSpan.FromMilliseconds(_options.BreakdownLockTimeoutMs));

                cached = await GetAsync<T>(key);
                if (cached != null) return cached;

                // 等待后仍无缓存（持锁线程可能异常），降级直接查库
                _logger.LogWarning("[Redis] 击穿防护：等待超时，降级执行工厂 {Key}", key);
                var fallbackValue = await factory();
                if (fallbackValue != null)
                    await SetAsync(key, fallbackValue, expiration);
                return fallbackValue;
            }
        }

        // ====== 未启用锁的简单路径 ======
        {
            var value = await factory();

            if (value != null)
                await SetAsync(key, value, expiration);
            else if (_options.CacheNullValues)
            {
                var nullExpiry = TimeSpan.FromMinutes(_options.NullValueExpirationMinutes);
                await Db.StringSetAsync(PrefixKey(key), NullSentinel, nullExpiry);
            }

            return value;
        }
    }

    public async Task<bool> ExpireAsync(string key, TimeSpan expiration)
    {
        return await Db.KeyExpireAsync(PrefixKey(key), ApplyJitter(expiration));
    }

    public async Task<long> IncrementAsync(string key, long value = 1)
    {
        return await Db.StringIncrementAsync(PrefixKey(key), value);
    }

    public async Task<long> DecrementAsync(string key, long value = 1)
    {
        return await Db.StringDecrementAsync(PrefixKey(key), value);
    }
}
