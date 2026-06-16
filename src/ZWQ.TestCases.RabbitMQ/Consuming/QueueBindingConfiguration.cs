namespace ZWQ.TestCases.RabbitMQ.Consuming;

/// <summary>
/// 队列与交换器绑定配置（每个消费者独立配置）
/// </summary>
public class QueueBindingConfiguration
{
    public string QueueName { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string DeadLetterExchangeName { get; set; } = string.Empty;
    public string DeadLetterQueueName { get; set; } = string.Empty;
    public string DeadLetterRoutingKey { get; set; } = string.Empty;
    public int MaxRetryCount { get; set; } = 3;
}
