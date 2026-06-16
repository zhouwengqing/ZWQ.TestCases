namespace WyInfo.RabbitMQ.Sample.Models;

public class OrderSubmittedEvent
{
    public Guid OrderId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<string> Items { get; set; } = new();
}

public class PaymentCompletedEvent
{
    public Guid PaymentId { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "WeChat";
}

public class NotificationEvent
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = "DingTalk";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> TargetUsers { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ====== 请求 DTO ======

public class CreateOrderRequest
{
    public string CustomerEmail { get; set; } = "test@example.com";
    public decimal Amount { get; set; } = 99.9m;
    public List<string> Items { get; set; } = new() { "商品A", "商品B" };
}

public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; } = 99.9m;
    public string Method { get; set; } = "WeChat";
}

public class SendNotificationRequest
{
    public string Channel { get; set; } = "DingTalk";
    public string Title { get; set; } = "测试通知";
    public string Content { get; set; } = "这是一条测试消息";
    public List<string> TargetUsers { get; set; } = new() { "user1", "user2" };
}
