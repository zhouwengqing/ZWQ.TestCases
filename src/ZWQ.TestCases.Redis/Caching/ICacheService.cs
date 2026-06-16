using System;
using System.Threading.Tasks;

namespace ZWQ.TestCases.Redis.Caching;

/// <summary>
/// 分布式缓存服务接口
/// 提供类型安全的缓存操作，支持 JSON 序列化
/// </summary>
public interface ICacheService
{
    // ====== 基础操作 ======

    /// <summary>获取缓存值（反序列化为指定类型）</summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>设置缓存值（序列化为 JSON）</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>获取字符串值</summary>
    Task<string?> GetStringAsync(string key);

    /// <summary>设置字符串值</summary>
    Task SetStringAsync(string key, string value, TimeSpan? expiration = null);

    // ====== 删除与检查 ======

    /// <summary>删除缓存</summary>
    Task<bool> RemoveAsync(string key);

    /// <summary>检查缓存是否存在</summary>
    Task<bool> ExistsAsync(string key);

    // ====== 高级操作 ======

    /// <summary>获取缓存，不存在则通过工厂方法加载并缓存</summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class;

    /// <summary>设置过期时间</summary>
    Task<bool> ExpireAsync(string key, TimeSpan expiration);

    /// <summary>原子递增</summary>
    Task<long> IncrementAsync(string key, long value = 1);

    /// <summary>原子递减</summary>
    Task<long> DecrementAsync(string key, long value = 1);
}
