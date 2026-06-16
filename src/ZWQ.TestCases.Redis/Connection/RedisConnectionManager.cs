using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZWQ.TestCases.Redis.Options;

namespace ZWQ.TestCases.Redis.Connection;

/// <summary>
/// Redis 连接管理器（单例）
/// 整个应用生命周期内维护一个 IConnectionMultiplexer，支持自动重连
/// </summary>
public class RedisConnectionManager : IDisposable
{
    private readonly Lazy<IConnectionMultiplexer> _connection;
    private readonly ILogger<RedisConnectionManager> _logger;
    private readonly RedisOptions _options;

    /// <summary>连接是否可用</summary>
    public bool IsConnected => _connection.IsValueCreated && _connection.Value.IsConnected;

    public RedisConnectionManager(IOptions<RedisOptions> options, ILogger<RedisConnectionManager> logger)
    {
        _logger = logger;
        _options = options.Value;

        _connection = new Lazy<IConnectionMultiplexer>(() =>
        {
            var connectionString = _options.BuildConnectionString();

            for (int attempt = 1; attempt <= _options.ConnectRetryCount; attempt++)
            {
                try
                {
                    var conn = ConnectionMultiplexer.Connect(connectionString);
                    RegisterEvents(conn);
                    _logger.LogInformation("[Redis] 连接成功 ({Host}:{Port})", _options.Host, _options.Port);
                    return conn;
                }
                catch (Exception ex) when (attempt < _options.ConnectRetryCount)
                {
                    _logger.LogWarning(ex,
                        "[Redis] 第 {Attempt}/{Max} 次连接失败，{Interval}秒后重试...",
                        attempt, _options.ConnectRetryCount, _options.ConnectRetryIntervalSeconds);
                    Thread.Sleep(TimeSpan.FromSeconds(_options.ConnectRetryIntervalSeconds));
                }
            }

            throw new InvalidOperationException(
                $"[Redis] 重试 {_options.ConnectRetryCount} 次后仍无法连接到 {_options.Host}:{_options.Port}");
        });
    }

    /// <summary>获取底层 IConnectionMultiplexer</summary>
    public IConnectionMultiplexer GetConnection() => _connection.Value;

    /// <summary>获取指定数据库（默认使用配置中的 DefaultDatabase）</summary>
    public IDatabase GetDatabase(int? db = null) => _connection.Value.GetDatabase(db ?? _options.DefaultDatabase);

    /// <summary>获取服务器实例（用于管理操作）</summary>
    public IServer GetServer() => _connection.Value.GetServer(_options.Host, _options.Port);

    /// <summary>获取发布/订阅通道</summary>
    public ISubscriber GetSubscriber() => _connection.Value.GetSubscriber();

    private void RegisterEvents(IConnectionMultiplexer conn)
    {
        conn.ConnectionRestored += (sender, e) =>
            _logger.LogInformation("[Redis] 连接已恢复: {EndPoint}, 类型: {ConnectionType}",
                e.EndPoint, e.ConnectionType);

        conn.ConnectionFailed += (sender, e) =>
            _logger.LogWarning("[Redis] 连接失败: {EndPoint}, 类型: {ConnectionType}, 原因: {FailureType}",
                e.EndPoint, e.ConnectionType, e.FailureType);

        conn.ErrorMessage += (sender, e) =>
            _logger.LogError("[Redis] 服务器错误: {EndPoint} - {Message}", e.EndPoint, e.Message);
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Close();
            _connection.Value.Dispose();
        }
    }
}
