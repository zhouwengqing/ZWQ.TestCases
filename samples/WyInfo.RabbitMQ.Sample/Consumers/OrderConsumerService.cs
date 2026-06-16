using Microsoft.Extensions.Options;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Consuming;
using WyInfo.RabbitMQ.Options;
using WyInfo.RabbitMQ.Sample.Models;

namespace WyInfo.RabbitMQ.Sample.Consumers;

public class OrderConsumerService : BaseMessageConsumer<OrderSubmittedEvent>
{
    public OrderConsumerService(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<OrderConsumerService> logger,
        IServiceScopeFactory scopeFactory)
        : base(connectionManager,
            new QueueBindingConfiguration
            {
                ExchangeName = "order_exchange",
                QueueName = "order_queue",
                RoutingKey = "order.created",
                DeadLetterExchangeName = options.Value.DeadLetterExchangeName,
                DeadLetterQueueName = "order_dlx_queue",
                DeadLetterRoutingKey = "order.created",
                MaxRetryCount = options.Value.MaxRetryCount
            },
            logger, scopeFactory, options) { }

    protected override async Task ProcessMessageAsync(OrderSubmittedEvent message)
    {
        _logger.LogInformation("【订单消费】处理订单 {OrderId}，客户: {Email}，金额: {Amount}，商品: {Items}",
            message.OrderId, message.CustomerEmail, message.Amount, string.Join(", ", message.Items));

        // 模拟业务处理（保存数据库、发送邮件等）
        await Task.Delay(1000);

        _logger.LogInformation("【订单消费】订单 {OrderId} 处理完成", message.OrderId);
    }

    protected override string GetMessageId(OrderSubmittedEvent message) => message.OrderId.ToString();
}
