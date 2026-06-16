using ZWQ.TestCases.RabbitMQ.Publishing;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Services;

public interface IOrderService
{
    OrderSubmittedEvent CreateOrder(CreateOrderRequest request);
}

public class OrderService : IOrderService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IMessagePublisher publisher, ILogger<OrderService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public OrderSubmittedEvent CreateOrder(CreateOrderRequest request)
    {
        var orderEvent = new OrderSubmittedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = request.CustomerEmail,
            Amount = request.Amount,
            Items = request.Items,
            Timestamp = DateTime.UtcNow
        };

        _publisher.Publish(orderEvent, exchangeName: "order_exchange", routingKey: "order.created");

        _logger.LogInformation("订单消息已发布: OrderId={OrderId}, Amount={Amount}", orderEvent.OrderId, orderEvent.Amount);

        return orderEvent;
    }
}
