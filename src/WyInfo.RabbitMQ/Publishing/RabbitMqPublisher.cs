using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Options;

namespace WyInfo.RabbitMQ.Publishing;

/// <summary>
/// RabbitMQ 消息发布者实现
/// 每次发布创建独立信道，用完后立即释放（using 模式）
/// </summary>
public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqOptions _options;

    public RabbitMqPublisher(RabbitMqConnectionManager connectionManager, IOptions<RabbitMqOptions> options)
    {
        _connection = connectionManager.GetConnection();
        _options = options.Value;
    }

    public void Publish<T>(T message, string? exchangeName = null, string? routingKey = null)
    {
        var exchange = exchangeName ?? _options.ExchangeName;
        var key = routingKey ?? _options.RoutingKey;

        using var channel = _connection.CreateModel();

        channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Topic, durable: true);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = typeof(T).Name;

        channel.BasicPublish(exchange: exchange, routingKey: key, basicProperties: properties, body: body);
    }

    public void Dispose() { }
}
