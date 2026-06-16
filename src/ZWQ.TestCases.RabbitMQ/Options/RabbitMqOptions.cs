using System;
using System.Collections.Generic;
using System.Text;

namespace ZWQ.TestCases.RabbitMQ.Options;

/// <summary>
/// RabbitMQ 连接及拓扑配置
/// 从 appsettings.json 的 "RabbitMq" 节点绑定
/// </summary>
public class RabbitMqOptions
{
    // ---------- 连接参数 ----------
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;

    // ---------- 主交换机/队列（默认值，可被消费者覆盖） ----------
    public string ExchangeName { get; set; } = "default_exchange";
    public string QueueName { get; set; } = "default_queue";
    public string RoutingKey { get; set; } = "default.routing";

    // ---------- 死信相关 ----------
    public string DeadLetterExchangeName { get; set; } = "dlx_exchange";
    public string DeadLetterQueueName { get; set; } = "dlx_queue";
    public string DeadLetterRoutingKey { get; set; } = "dead-letter";

    // ---------- 重试策略 ----------
    /// <summary>业务逻辑最大重试次数（指数退避）</summary>
    public int MaxRetryCount { get; set; } = 3;

    // ---------- 连接恢复 ----------
    /// <summary>启动时连接 RabbitMQ 的最大重试次数</summary>
    public int ConnectionRetryCount { get; set; } = 5;
    /// <summary>启动时每次连接重试的间隔（秒）</summary>
    public int ConnectionRetryIntervalSeconds { get; set; } = 5;
    /// <summary>连接断开后，消费者自动恢复的延迟（秒）</summary>
    public int RecoveryDelaySeconds { get; set; } = 5;
}
