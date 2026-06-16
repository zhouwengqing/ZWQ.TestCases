namespace ZWQ.TestCases.RabbitMQ.Publishing;

/// <summary>
/// 消息发布者接口
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// 发布消息到 RabbitMQ
    /// </summary>
    /// <param name="message">消息实例</param>
    /// <param name="exchangeName">交换器名称，不传则使用配置的默认值</param>
    /// <param name="routingKey">路由键，不传则使用配置的默认值</param>
    void Publish<T>(T message, string? exchangeName = null, string? routingKey = null);
}
