using Microsoft.Extensions.Options;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Consuming;
using WyInfo.RabbitMQ.Options;
using WyInfo.RabbitMQ.Sample.Models;

namespace WyInfo.RabbitMQ.Sample.Consumers;

public class NotificationConsumerService : BaseMessageConsumer<NotificationEvent>
{
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

    protected override async Task ProcessMessageAsync(NotificationEvent message)
    {
        _logger.LogInformation("【通知消费】发送 {Channel} 通知: {Title}，目标: {Users}",
            message.Channel, message.Title, string.Join(", ", message.TargetUsers));

        await Task.Delay(300);

        _logger.LogInformation("【通知消费】通知 {Id} 发送完成", message.NotificationId);
    }

    protected override string GetMessageId(NotificationEvent message) => message.NotificationId.ToString();
}
