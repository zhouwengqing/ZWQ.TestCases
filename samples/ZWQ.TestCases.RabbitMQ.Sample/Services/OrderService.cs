using ZWQ.TestCases.RabbitMQ.Publishing;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Services;

/// <summary>
/// 订单服务接口
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// 创建订单并发布 MQ 消息到 order_exchange
    /// </summary>
    /// <param name="request">创建订单请求</param>
    /// <returns>已创建的订单事件</returns>
    OrderSubmittedEvent CreateOrder(CreateOrderRequest request);
}

/// <summary>
/// 订单服务实现 — 构建订单事件并发布到 RabbitMQ
/// </summary>
public class OrderService : IOrderService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="publisher">MQ 消息发布者</param>
    /// <param name="logger">日志记录器</param>
    public OrderService(IMessagePublisher publisher, ILogger<OrderService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
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
