using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZWQ.TestCases.RabbitMQ.Connection;
using ZWQ.TestCases.RabbitMQ.Idempotency;
using ZWQ.TestCases.RabbitMQ.Options;

namespace ZWQ.TestCases.RabbitMQ.Consuming;

/// <summary>
/// 消息消费者泛型基类
/// 封装 RabbitMQ 连接、消费、ACK/NACK、幂等、重试、死信、连接恢复、健康检查等通用逻辑
/// 子类只需实现 ProcessMessageAsync（业务逻辑）和 GetMessageId（幂等 ID）
/// </summary>
public abstract class BaseMessageConsumer<TMessage> : BackgroundService
{
    protected readonly RabbitMqConnectionManager _connectionManager;
    protected readonly ILogger _logger;
    protected readonly QueueBindingConfiguration _config;
    protected readonly IServiceScopeFactory _scopeFactory;
    protected readonly RabbitMqOptions _mqOptions;
    protected IModel? _channel;

    private readonly object _channelLock = new();
    private bool _recovering;

    protected BaseMessageConsumer(
        RabbitMqConnectionManager connectionManager,
        QueueBindingConfiguration config,
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> mqOptions)
    {
        _connectionManager = connectionManager;
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _mqOptions = mqOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connectionManager.ConnectionShutdownEvent += OnConnectionShutdown;

        if (!TryStartConsuming())
            _logger.LogError("[{Queue}] 初始启动失败，将在健康检查中持续重试", _config.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }

            bool needsRecovery;
            lock (_channelLock) { needsRecovery = _channel == null || !_channel.IsOpen; }

            if (needsRecovery && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("[{Queue}] 健康检查：消费者不活跃，尝试恢复...", _config.QueueName);
                TryRecover();
            }
        }
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("[{Queue}] 检测到 RabbitMQ 连接断开 ({Reason})，{Delay}秒后尝试恢复...",
            _config.QueueName, e.ReplyText, _mqOptions.RecoveryDelaySeconds);

        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(_mqOptions.RecoveryDelaySeconds));
            TryRecover();
        });
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

                _channel.ExchangeDeclare(_config.ExchangeName, ExchangeType.Topic, durable: true);
                _channel.ExchangeDeclare(_config.DeadLetterExchangeName, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_config.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_config.DeadLetterQueueName, _config.DeadLetterExchangeName, _config.DeadLetterRoutingKey);

                var queueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", _config.DeadLetterExchangeName },
                    { "x-dead-letter-routing-key", _config.DeadLetterRoutingKey }
                };
                _channel.QueueDeclare(_config.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
                _channel.QueueBind(_config.QueueName, _config.ExchangeName, _config.RoutingKey);
                _channel.BasicQos(0, 1, false);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += OnMessageReceived;
                _channel.BasicConsume(queue: _config.QueueName, autoAck: false, consumer: consumer);

                _logger.LogInformation("[{Queue}] 消费者已启动，消息类型: {MessageType}",
                    _config.QueueName, typeof(TMessage).Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Queue}] 启动消费者失败", _config.QueueName);
                return false;
            }
        }
    }

    private async void OnMessageReceived(object? model, BasicDeliverEventArgs ea)
    {
        var deliveryTag = ea.DeliveryTag;
        var body = ea.Body.ToArray();
        var messageText = Encoding.UTF8.GetString(body);
        var sw = Stopwatch.StartNew();

        try
        {
            var message = JsonSerializer.Deserialize<TMessage>(messageText);
            if (message == null)
            {
                _logger.LogWarning("[{Queue}] 无法解析的消息，已丢弃", _config.QueueName);
                SafeNack(deliveryTag);
                return;
            }

            // ====== 幂等抢占（先占位，再处理） ======
            string id = GetMessageId(message);
            if (!string.IsNullOrEmpty(id))
            {
                using var claimScope = _scopeFactory.CreateScope();
                var store = claimScope.ServiceProvider.GetRequiredService<IMessageIdempotencyStore>();

                if (!await store.TryClaimAsync(id, _config.QueueName, typeof(TMessage).Name, messageText))
                {
                    _logger.LogWarning("[{Queue}] 消息 {MessageId} 已被处理或正在处理，已跳过", _config.QueueName, id);
                    SafeAck(deliveryTag);
                    return;
                }
            }

            // ====== 执行业务逻辑（带重试） ======
            await ProcessMessageWithRetry(message);
            sw.Stop();

            // ====== 处理成功：更新占位记录 ======
            if (!string.IsNullOrEmpty(id))
            {
                using var completeScope = _scopeFactory.CreateScope();
                var store = completeScope.ServiceProvider.GetRequiredService<IMessageIdempotencyStore>();
                await store.CompleteAsync(id, _config.QueueName, 1, typeof(TMessage).Name, messageText, sw.ElapsedMilliseconds);
            }

            _logger.LogInformation("[{Queue}] 消息处理成功 MessageId={MessageId}，耗时 {ElapsedMs}ms",
                _config.QueueName, id, sw.ElapsedMilliseconds);
            SafeAck(deliveryTag);
        }
        catch (Exception ex)
        {
            sw.Stop();

            if (IsTransientError(ex))
            {
                _logger.LogWarning(ex, "[{Queue}] 检测到瞬态错误，30秒后重新入队", _config.QueueName);
                try { await Task.Delay(TimeSpan.FromSeconds(30)); } catch (OperationCanceledException) { }
                SafeRequeue(deliveryTag);
                return;
            }

            _logger.LogError(ex, "[{Queue}] 消息处理最终失败，进入死信队列", _config.QueueName);

            try
            {
                string? failId = null;
                try { var fm = JsonSerializer.Deserialize<TMessage>(messageText); if (fm != null) failId = GetMessageId(fm); } catch { }

                if (!string.IsNullOrEmpty(failId))
                {
                    using var failScope = _scopeFactory.CreateScope();
                    var store = failScope.ServiceProvider.GetRequiredService<IMessageIdempotencyStore>();
                    await store.CompleteAsync(failId, _config.QueueName, 2, typeof(TMessage).Name, messageText);
                }
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "[{Queue}] 写入失败记录时发生异常", _config.QueueName);
            }

            SafeNack(deliveryTag);
        }
    }

    private void SafeAck(ulong deliveryTag)
    {
        try { lock (_channelLock) { if (_channel?.IsOpen == true) _channel.BasicAck(deliveryTag, false); } }
        catch (Exception ex) { _logger.LogWarning(ex, "[{Queue}] ACK 失败", _config.QueueName); }
    }

    private void SafeNack(ulong deliveryTag)
    {
        try { lock (_channelLock) { if (_channel?.IsOpen == true) _channel.BasicNack(deliveryTag, false, requeue: false); } }
        catch (Exception ex) { _logger.LogWarning(ex, "[{Queue}] NACK 失败", _config.QueueName); }
    }

    private void SafeRequeue(ulong deliveryTag)
    {
        try { lock (_channelLock) { if (_channel?.IsOpen == true) _channel.BasicNack(deliveryTag, false, requeue: true); } }
        catch (Exception ex) { _logger.LogWarning(ex, "[{Queue}] Requeue 失败", _config.QueueName); }
    }

    private static bool IsTransientError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var typeName = current.GetType().Name;
            if (typeName is "SqlException" or "NpgsqlException" or "MySqlException") return true;

            var msg = current.Message ?? "";
            if (msg.Contains("与 SQL Server 建立连接") || msg.Contains("无法打开与 SQL Server 的连接") ||
                msg.Contains("A network-related or instance-specific error") || msg.Contains("找不到网络路径") ||
                msg.Contains("连接超时") || msg.Contains("Connection timeout") || msg.Contains("The wait operation timed out"))
                return true;
        }
        return ex is TimeoutException;
    }

    private void TryRecover()
    {
        if (_recovering) return;
        _recovering = true;
        try
        {
            if (TryStartConsuming())
                _logger.LogInformation("[{Queue}] 消费者恢复成功", _config.QueueName);
            else
                _logger.LogWarning("[{Queue}] 消费者恢复失败，将在下次健康检查时重试", _config.QueueName);
        }
        catch (Exception ex) { _logger.LogError(ex, "[{Queue}] 恢复过程中发生异常", _config.QueueName); }
        finally { _recovering = false; }
    }

    private void CleanupChannel()
    {
        try { if (_channel != null) { if (_channel.IsOpen) _channel.Close(); _channel.Dispose(); } } catch { }
        _channel = null;
    }

    private async Task ProcessMessageWithRetry(TMessage message)
    {
        int attempt = 0;
        while (attempt < _config.MaxRetryCount)
        {
            try { attempt++; await ProcessMessageAsync(message); return; }
            catch (Exception ex) when (attempt < _config.MaxRetryCount)
            {
                _logger.LogWarning(ex, "[{Queue}] 第 {Attempt}/{Max} 次处理失败，准备重试...",
                    _config.QueueName, attempt, _config.MaxRetryCount);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
        throw new Exception($"重试 {_config.MaxRetryCount} 次后仍然失败");
    }

    /// <summary>具体业务逻辑，由子类实现</summary>
    protected abstract Task ProcessMessageAsync(TMessage message);

    /// <summary>获取消息幂等 ID，子类必须实现</summary>
    protected abstract string GetMessageId(TMessage message);

    public override void Dispose() { CleanupChannel(); base.Dispose(); }
}
