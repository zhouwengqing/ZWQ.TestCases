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
/// 支持 JSON 序列化、键前缀、默认过期时间
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await Db.StringGetAsync(PrefixKey(key));
            if (value.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
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
        var expiry = expiration ?? DefaultExpiration;

        await Db.StringSetAsync(PrefixKey(key), json, expiry);
    }

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await Db.StringGetAsync(PrefixKey(key));
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
    {
        var expiry = expiration ?? DefaultExpiration;
        await Db.StringSetAsync(PrefixKey(key), value, expiry);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        return await Db.KeyDeleteAsync(PrefixKey(key));
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await Db.KeyExistsAsync(PrefixKey(key));
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class
    {
        var cached = await GetAsync<T>(key);
        if (cached != null) return cached;

        var value = await factory();
        if (value != null)
            await SetAsync(key, value, expiration);

        return value;
    }

    public async Task<bool> ExpireAsync(string key, TimeSpan expiration)
    {
        return await Db.KeyExpireAsync(PrefixKey(key), expiration);
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
