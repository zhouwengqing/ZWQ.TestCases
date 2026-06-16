using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Options;

namespace WyInfo.RabbitMQ.Consuming;

/// <summary>
/// 死信队列消费者
/// 监听死信队列，记录失败消息并触发告警（人工介入）
/// 支持连接断开后自动恢复 + 健康检查
/// </summary>
public class DeadLetterConsumerService : BackgroundService
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<DeadLetterConsumerService> _logger;
    private IModel? _channel;
    private readonly object _channelLock = new();
    private bool _recovering;

    public DeadLetterConsumerService(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<DeadLetterConsumerService> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connectionManager.ConnectionShutdownEvent += OnConnectionShutdown;

        if (!TryStartConsuming())
            _logger.LogError("[死信队列] 初始启动失败，将在健康检查中持续重试");

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }

            bool needsRecovery;
            lock (_channelLock) { needsRecovery = _channel == null || !_channel.IsOpen; }

            if (needsRecovery)
            {
                _logger.LogWarning("[死信队列] 健康检查：消费者不活跃，尝试恢复...");
                TryRecover();
            }
        }
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("[死信队列] 连接断开 ({Reason})，{Delay}秒后恢复...", e.ReplyText, _options.RecoveryDelaySeconds);
        Task.Run(async () => { await Task.Delay(TimeSpan.FromSeconds(_options.RecoveryDelaySeconds)); TryRecover(); });
    }

    private bool TryStartConsuming()
    {
        lock (_channelLock)
        {
            try
            {
                CleanupChannel();
                var connection = _connectionManager.GetConnection();
                if (!connection.IsOpen) return false;

                _channel = connection.CreateModel();
                _channel.ExchangeDeclare(_options.DeadLetterExchangeName, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_options.DeadLetterQueueName, _options.DeadLetterExchangeName, _options.DeadLetterRoutingKey);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogError("【死信】收到无法处理的消息，请人工介入。内容：{Message}", message);

                    try { lock (_channelLock) { if (_channel?.IsOpen == true) _channel.BasicAck(ea.DeliveryTag, false); } }
                    catch (Exception ex) { _logger.LogWarning(ex, "[死信队列] ACK 失败"); }
                };

                _channel.BasicConsume(_options.DeadLetterQueueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("[死信队列] 消费者已启动");
                return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "[死信队列] 启动失败"); return false; }
        }
    }

    private void TryRecover()
    {
        if (_recovering) return;
        _recovering = true;
        try { if (TryStartConsuming()) _logger.LogInformation("[死信队列] 恢复成功"); }
        catch (Exception ex) { _logger.LogError(ex, "[死信队列] 恢复异常"); }
        finally { _recovering = false; }
    }

    private void CleanupChannel()
    {
        try { if (_channel != null) { if (_channel.IsOpen) _channel.Close(); _channel.Dispose(); } } catch { }
        _channel = null;
    }

    public override void Dispose() { CleanupChannel(); base.Dispose(); }
}
