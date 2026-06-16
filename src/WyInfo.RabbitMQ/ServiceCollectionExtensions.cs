using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Idempotency;
using WyInfo.RabbitMQ.Options;
using WyInfo.RabbitMQ.Publishing;

namespace WyInfo.RabbitMQ;

/// <summary>
/// RabbitMQ 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 RabbitMQ 基础设施（连接管理器、发布者、幂等存储）
    /// 消费者需单独通过 AddHostedService 注册
    /// </summary>
    /// <typeparam name="TDbContext">包含 MqProcessedMessage 的 DbContext 类型</typeparam>
    public static IServiceCollection AddWyInfoRabbitMq<TDbContext>(
        this IServiceCollection services,
        Action<RabbitMqOptions>? configureOptions = null)
        where TDbContext : DbContext
    {
        if (configureOptions != null)
            services.Configure(configureOptions);

        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddScoped<IMessageIdempotencyStore, EfMessageIdempotencyStore<TDbContext>>();

        return services;
    }
}
