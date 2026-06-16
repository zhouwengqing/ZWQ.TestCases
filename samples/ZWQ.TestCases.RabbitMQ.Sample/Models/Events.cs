namespace ZWQ.TestCases.RabbitMQ.Sample.Models;

/// <summary>
/// 订单提交事件 — 当用户下单后发布到 order_exchange
/// </summary>
public class OrderSubmittedEvent
{
    /// <summary>订单唯一标识</summary>
    public Guid OrderId { get; set; } = Guid.NewGuid();

    /// <summary>事件发生时间（UTC）</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>客户邮箱</summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>订单金额</summary>
    public decimal Amount { get; set; }

    /// <summary>购买商品列表</summary>
    public List<string> Items { get; set; } = new();
}

/// <summary>
/// 支付完成事件 — 支付成功后发布到 payment_exchange
/// </summary>
public class PaymentCompletedEvent
{
    /// <summary>支付单唯一标识</summary>
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    /// <summary>关联的订单 ID</summary>
    public Guid OrderId { get; set; }

    /// <summary>支付金额</summary>
    public decimal Amount { get; set; }

    /// <summary>支付完成时间（UTC）</summary>
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    /// <summary>支付方式（WeChat / Alipay / BankCard）</summary>
    public string Method { get; set; } = "WeChat";
}

/// <summary>
/// 通知事件 — 发布到 notification_exchange 触发多渠道推送
/// </summary>
public class NotificationEvent
{
    /// <summary>通知唯一标识</summary>
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    /// <summary>推送渠道（DingTalk / Feishu / Email / SMS）</summary>
    public string Channel { get; set; } = "DingTalk";

    /// <summary>通知标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>通知正文</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>目标用户列表</summary>
    public List<string> TargetUsers { get; set; } = new();

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ====== 请求 DTO ======

/// <summary>
/// 创建订单请求
/// </summary>
public class CreateOrderRequest
{
    /// <summary>客户邮箱</summary>
    public string CustomerEmail { get; set; } = "test@example.com";

    /// <summary>订单金额</summary>
    public decimal Amount { get; set; } = 99.9m;

    /// <summary>商品列表</summary>
    public List<string> Items { get; set; } = new() { "商品A", "商品B" };
}

/// <summary>
/// 创建支付请求
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>关联的订单 ID</summary>
    public Guid OrderId { get; set; }

    /// <summary>支付金额</summary>
    public decimal Amount { get; set; } = 99.9m;

    /// <summary>支付方式（WeChat / Alipay / BankCard）</summary>
    public string Method { get; set; } = "WeChat";
}

/// <summary>
/// 发送通知请求
/// </summary>
public class SendNotificationRequest
{
    /// <summary>推送渠道（DingTalk / Feishu / Email / SMS）</summary>
    public string Channel { get; set; } = "DingTalk";

    /// <summary>通知标题</summary>
    public string Title { get; set; } = "测试通知";

    /// <summary>通知正文</summary>
    public string Content { get; set; } = "这是一条测试消息";

    /// <summary>目标用户列表</summary>
    public List<string> TargetUsers { get; set; } = new() { "user1", "user2" };
}
