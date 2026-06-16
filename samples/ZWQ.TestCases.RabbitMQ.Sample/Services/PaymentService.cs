using ZWQ.TestCases.RabbitMQ.Publishing;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Services;

/// <summary>
/// 支付服务接口
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// 创建支付并发布 MQ 消息到 payment_exchange
    /// </summary>
    /// <param name="request">创建支付请求</param>
    /// <returns>已创建的支付事件</returns>
    PaymentCompletedEvent CreatePayment(CreatePaymentRequest request);
}

/// <summary>
/// 支付服务实现 — 构建支付事件并发布到 RabbitMQ
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<PaymentService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="publisher">MQ 消息发布者</param>
    /// <param name="logger">日志记录器</param>
    public PaymentService(IMessagePublisher publisher, ILogger<PaymentService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public PaymentCompletedEvent CreatePayment(CreatePaymentRequest request)
    {
        var paymentEvent = new PaymentCompletedEvent
        {
            PaymentId = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = request.Amount,
            Method = request.Method,
            PaidAt = DateTime.UtcNow
        };

        _publisher.Publish(paymentEvent, exchangeName: "payment_exchange", routingKey: "payment.completed");

        _logger.LogInformation("支付消息已发布: PaymentId={PaymentId}, OrderId={OrderId}", paymentEvent.PaymentId, paymentEvent.OrderId);

        return paymentEvent;
    }
}
