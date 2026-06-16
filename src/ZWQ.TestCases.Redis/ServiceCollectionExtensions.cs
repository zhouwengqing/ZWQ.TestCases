using System;
using Microsoft.Extensions.DependencyInjection;
using ZWQ.TestCases.Redis.BloomFilter;
using ZWQ.TestCases.Redis.Caching;
using ZWQ.TestCases.Redis.Connection;
using ZWQ.TestCases.Redis.Locking;
using ZWQ.TestCases.Redis.Monitoring;
using ZWQ.TestCases.Redis.Options;

namespace ZWQ.TestCases.Redis;

/// <summary>
/// Redis 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Redis 基础设施（连接管理器、缓存服务、分布式锁、布隆过滤器、心跳监控）
    /// </summary>
    public static IServiceCollection AddZwqRedis(
        this IServiceCollection services,
        Action<RedisOptions>? configureOptions = null)
    {
        if (configureOptions != null)
            services.Configure(configureOptions);

        // 核心连接
        services.AddSingleton<RedisConnectionManager>();

        // 缓存 + 锁
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ILockService, RedisLockService>();

        // 布隆过滤器（穿透防护第一线）
        services.AddSingleton<IBloomFilter, RedisBloomFilter>();

        // 心跳监控（BackgroundService）
        services.AddSingleton<RedisHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<RedisHealthMonitor>());

        return services;
    }
}
