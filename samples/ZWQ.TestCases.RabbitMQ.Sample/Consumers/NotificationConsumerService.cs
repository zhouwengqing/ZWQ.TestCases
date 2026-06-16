using Microsoft.Extensions.Options;
using ZWQ.TestCases.RabbitMQ.Connection;
using ZWQ.TestCases.RabbitMQ.Consuming;
using ZWQ.TestCases.RabbitMQ.Options;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Consumers;

/// <summary>
/// 通知消息消费者 — 监听 notification_queue，处理多渠道推送事件
/// 继承 BaseMessageConsumer，自动获得幂等、重试、死信、连接恢复等能力
/// </summary>
public class NotificationConsumerService : BaseMessageConsumer<NotificationEvent>
{
    /// <summary>
    /// 构造函数 — 配置队列拓扑（exchange / queue / routing key / DLX）
    /// </summary>
    public NotificationConsumerService(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<NotificationConsumerService> logger,
        IServiceScopeFactory scopeFactory)
        : base(connectionManager,
            new QueueBindingConfiguration
            {
                ExchangeName = "notification_exchange",
                QueueName = "notification_queue",
                RoutingKey = "notification.send",
                DeadLetterExchangeName = options.Value.DeadLetterExchangeName,
                DeadLetterQueueName = "notification_dlx_queue",
                DeadLetterRoutingKey = "notification.send",
                MaxRetryCount = options.Value.MaxRetryCount
            },
            logger, scopeFactory, options) { }

    /// <summary>
    /// 处理通知消息（模拟调用钉钉/飞书/邮件等推送 API）
    /// </summary>
    /// <param name="message">通知事件</param>
    protected override async Task ProcessMessageAsync(NotificationEvent message)
    {
        _logger.LogInformation("【通知消费】发送 {Channel} 通知: {Title}，目标: {Users}",
            message.Channel, message.Title, string.Join(", ", message.TargetUsers));

        await Task.Delay(300);

        _logger.LogInformation("【通知消费】通知 {Id} 发送完成", message.NotificationId);
    }

    /// <summary>
    /// 获取消息幂等 ID（使用 NotificationId 作为唯一标识）
    /// </summary>
    protected override string GetMessageId(NotificationEvent message) => message.NotificationId.ToString();
}
