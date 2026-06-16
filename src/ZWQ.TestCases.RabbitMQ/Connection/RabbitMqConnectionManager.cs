using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ZWQ.TestCases.RabbitMQ.Options;

namespace ZWQ.TestCases.RabbitMQ.Connection;

/// <summary>
/// RabbitMQ 连接管理器（单例）
/// 整个应用生命周期内只创建一条 TCP 长连接，并开启自动恢复
/// 启动时支持连接重试，避免 RabbitMQ 不可达时应用直接崩溃
/// </summary>
public class RabbitMqConnectionManager : IDisposable
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqConnectionManager> _logger;

    /// <summary>连接是否存活</summary>
    public bool IsConnected => _connection?.IsOpen == true;

    public RabbitMqConnectionManager(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionManager> logger)
    {
        _logger = logger;
        var opt = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = opt.Host,
            VirtualHost = opt.VirtualHost,
            UserName = opt.Username,
            Password = opt.Password,
            Port = opt.Port,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            TopologyRecoveryEnabled = true
        };

        var maxRetry = opt.ConnectionRetryCount;
        var retryInterval = TimeSpan.FromSeconds(opt.ConnectionRetryIntervalSeconds);

        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection();
                _logger.LogInformation("[RabbitMQ] 连接成功 ({Host}:{Port})", opt.Host, opt.Port);
                break;
            }
            catch (Exception ex) when (attempt < maxRetry)
            {
                _logger.LogWarning(ex,
                    "[RabbitMQ] 第 {Attempt}/{Max} 次连接失败，{Interval}秒后重试...",
                    attempt, maxRetry, retryInterval.TotalSeconds);
                System.Threading.Thread.Sleep(retryInterval);
            }
        }

        if (_connection == null)
        {
            throw new InvalidOperationException(
                $"[RabbitMQ] 重试 {maxRetry} 次后仍无法连接到 {opt.Host}:{opt.Port}");
        }

        _connection.ConnectionShutdown += (sender, args) =>
        {
            _logger.LogWarning("[RabbitMQ] 连接断开 - Reason: {Reason}", args.ReplyText);
            ConnectionShutdownEvent?.Invoke(this, args);
        };

        _connection.CallbackException += (sender, args) =>
        {
            _logger.LogError(args.Exception, "[RabbitMQ] 回调异常");
        };
    }

    /// <summary>连接断开事件（消费者可订阅此事件触发重建）</summary>
    public event EventHandler<ShutdownEventArgs>? ConnectionShutdownEvent;

    /// <summary>获取已建立的连接</summary>
    public IConnection GetConnection() => _connection;

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
