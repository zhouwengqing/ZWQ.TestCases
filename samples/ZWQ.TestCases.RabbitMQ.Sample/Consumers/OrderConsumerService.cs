using Microsoft.Extensions.Options;
using ZWQ.TestCases.RabbitMQ.Connection;
using ZWQ.TestCases.RabbitMQ.Consuming;
using ZWQ.TestCases.RabbitMQ.Options;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Consumers;

/// <summary>
/// 订单消息消费者 — 监听 order_queue，处理订单创建事件
/// 继承 BaseMessageConsumer，自动获得幂等、重试、死信、连接恢复等能力
/// </summary>
public class OrderConsumerService : BaseMessageConsumer<OrderSubmittedEvent>
{
    /// <summary>
    /// 构造函数 — 配置队列拓扑（exchange / queue / routing key / DLX）
    /// </summary>
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

    /// <summary>
    /// 处理订单消息（模拟保存数据库、发送邮件等业务逻辑）
    /// </summary>
    /// <param name="message">订单提交事件</param>
    protected override async Task ProcessMessageAsync(OrderSubmittedEvent message)
    {
        _logger.LogInformation("【订单消费】处理订单 {OrderId}，客户: {Email}，金额: {Amount}，商品: {Items}",
            message.OrderId, message.CustomerEmail, message.Amount, string.Join(", ", message.Items));

        // 模拟业务处理（保存数据库、发送邮件等）
        await Task.Delay(1000);

        _logger.LogInformation("【订单消费】订单 {OrderId} 处理完成", message.OrderId);
    }

    /// <summary>
    /// 获取消息幂等 ID（使用 OrderId 作为唯一标识）
    /// </summary>
    protected override string GetMessageId(OrderSubmittedEvent message) => message.OrderId.ToString();
}
