using ZWQ.TestCases.RabbitMQ.Publishing;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Services;

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 发送通知并发布 MQ 消息到 notification_exchange
    /// </summary>
    /// <param name="request">发送通知请求</param>
    /// <returns>已创建的通知事件</returns>
    NotificationEvent SendNotification(SendNotificationRequest request);
}

/// <summary>
/// 通知服务实现 — 构建通知事件并发布到 RabbitMQ
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="publisher">MQ 消息发布者</param>
    /// <param name="logger">日志记录器</param>
    public NotificationService(IMessagePublisher publisher, ILogger<NotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationEvent SendNotification(SendNotificationRequest request)
    {
        var notificationEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Channel = request.Channel,
            Title = request.Title,
            Content = request.Content,
            TargetUsers = request.TargetUsers,
            CreatedAt = DateTime.UtcNow
        };

        _publisher.Publish(notificationEvent, exchangeName: "notification_exchange", routingKey: "notification.send");

        _logger.LogInformation("通知消息已发布: NotificationId={Id}, Channel={Channel}", notificationEvent.NotificationId, notificationEvent.Channel);

        return notificationEvent;
    }
}
