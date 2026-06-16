using ZWQ.TestCases.RabbitMQ.Publishing;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Services;

public interface INotificationService
{
    NotificationEvent SendNotification(SendNotificationRequest request);
}

public class NotificationService : INotificationService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IMessagePublisher publisher, ILogger<NotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

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
