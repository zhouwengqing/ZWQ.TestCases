using Microsoft.Extensions.Options;
using ZWQ.TestCases.RabbitMQ.Connection;
using ZWQ.TestCases.RabbitMQ.Consuming;
using ZWQ.TestCases.RabbitMQ.Options;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Consumers;

/// <summary>
/// 支付消息消费者 — 监听 payment_queue，处理支付完成事件
/// 继承 BaseMessageConsumer，自动获得幂等、重试、死信、连接恢复等能力
/// </summary>
public class PaymentConsumerService : BaseMessageConsumer<PaymentCompletedEvent>
{
    /// <summary>
    /// 构造函数 — 配置队列拓扑（exchange / queue / routing key / DLX）
    /// </summary>
    public PaymentConsumerService(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<PaymentConsumerService> logger,
        IServiceScopeFactory scopeFactory)
        : base(connectionManager,
            new QueueBindingConfiguration
            {
                ExchangeName = "payment_exchange",
                QueueName = "payment_queue",
                RoutingKey = "payment.completed",
                DeadLetterExchangeName = options.Value.DeadLetterExchangeName,
                DeadLetterQueueName = "payment_dlx_queue",
                DeadLetterRoutingKey = "payment.completed",
                MaxRetryCount = options.Value.MaxRetryCount
            },
            logger, scopeFactory, options) { }

    /// <summary>
    /// 处理支付消息（模拟更新订单状态、发送支付通知等）
    /// </summary>
    /// <param name="message">支付完成事件</param>
    protected override async Task ProcessMessageAsync(PaymentCompletedEvent message)
    {
        _logger.LogInformation("【支付消费】处理支付 {PaymentId}，订单: {OrderId}，金额: {Amount}，方式: {Method}",
            message.PaymentId, message.OrderId, message.Amount, message.Method);

        await Task.Delay(500);

        _logger.LogInformation("【支付消费】支付 {PaymentId} 处理完成", message.PaymentId);
    }

    /// <summary>
    /// 获取消息幂等 ID（使用 PaymentId 作为唯一标识）
    /// </summary>
    protected override string GetMessageId(PaymentCompletedEvent message) => message.PaymentId.ToString();
}
