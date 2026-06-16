using WyInfo.RabbitMQ.Publishing;
using WyInfo.RabbitMQ.Sample.Models;

namespace WyInfo.RabbitMQ.Sample.Services;

public interface IPaymentService
{
    PaymentCompletedEvent CreatePayment(CreatePaymentRequest request);
}

public class PaymentService : IPaymentService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IMessagePublisher publisher, ILogger<PaymentService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

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
