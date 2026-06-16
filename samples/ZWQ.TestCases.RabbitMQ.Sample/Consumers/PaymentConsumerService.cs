using Microsoft.Extensions.Options;
using ZWQ.TestCases.RabbitMQ.Connection;
using ZWQ.TestCases.RabbitMQ.Consuming;
using ZWQ.TestCases.RabbitMQ.Options;
using ZWQ.TestCases.RabbitMQ.Sample.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Consumers;

public class PaymentConsumerService : BaseMessageConsumer<PaymentCompletedEvent>
{
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

    protected override async Task ProcessMessageAsync(PaymentCompletedEvent message)
    {
        _logger.LogInformation("【支付消费】处理支付 {PaymentId}，订单: {OrderId}，金额: {Amount}，方式: {Method}",
            message.PaymentId, message.OrderId, message.Amount, message.Method);

        await Task.Delay(500);

        _logger.LogInformation("【支付消费】支付 {PaymentId} 处理完成", message.PaymentId);
    }

    protected override string GetMessageId(PaymentCompletedEvent message) => message.PaymentId.ToString();
}
