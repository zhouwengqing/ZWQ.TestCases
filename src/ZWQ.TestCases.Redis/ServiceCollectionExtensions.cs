using System;
using Microsoft.Extensions.DependencyInjection;
using ZWQ.TestCases.Redis.Caching;
using ZWQ.TestCases.Redis.Connection;
using ZWQ.TestCases.Redis.Locking;
using ZWQ.TestCases.Redis.Options;

namespace ZWQ.TestCases.Redis;

/// <summary>
/// Redis 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Redis 基础设施（连接管理器、缓存服务、分布式锁）
    /// </summary>
    public static IServiceCollection AddZwqRedis(
        this IServiceCollection services,
        Action<RedisOptions>? configureOptions = null)
    {
        if (configureOptions != null)
            services.Configure(configureOptions);

        services.AddSingleton<RedisConnectionManager>();
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ILockService, RedisLockService>();

        return services;
    }
}
