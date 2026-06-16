using System;
using System.Collections.Generic;

namespace ZWQ.TestCases.Redis.Options;

/// <summary>
/// Redis 连接及行为配置
/// 从 appsettings.json 的 "Redis" 节点绑定
/// </summary>
public class RedisOptions
{
    // ---------- 连接参数 ----------
    /// <summary>Redis 服务器地址</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Redis 端口</summary>
    public int Port { get; set; } = 6379;

    /// <summary>密码（无密码时留空）</summary>
    public string? Password { get; set; }

    /// <summary>是否使用 SSL</summary>
    public bool Ssl { get; set; } = false;

    /// <summary>默认数据库编号</summary>
    public int DefaultDatabase { get; set; } = 0;

    // ---------- 连接池与超时 ----------
    /// <summary>连接超时（毫秒）</summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>命令执行超时（毫秒）</summary>
    public int CommandTimeoutMs { get; set; } = 3000;

    /// <summary>连接重试次数</summary>
    public int ConnectRetryCount { get; set; } = 3;

    /// <summary>连接重试间隔（秒）</summary>
    public int ConnectRetryIntervalSeconds { get; set; } = 3;

    // ---------- 缓存默认行为 ----------
    /// <summary>缓存键前缀</summary>
    public string KeyPrefix { get; set; } = "";

    /// <summary>默认缓存过期时间（分钟）</summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>构建连接字符串</summary>
    public string BuildConnectionString()
    {
        var parts = new List<string>
        {
            $"{Host}:{Port}",
            $"connectTimeout={ConnectTimeoutMs}",
            $"syncTimeout={CommandTimeoutMs}",
            $"asyncTimeout={CommandTimeoutMs}",
            $"connectRetry={ConnectRetryCount}",
            $"abortConnect=false"
        };

        if (!string.IsNullOrEmpty(Password))
            parts.Add($"password={Password}");

        if (Ssl)
            parts.Add("ssl=true");

        if (DefaultDatabase > 0)
            parts.Add($"defaultDatabase={DefaultDatabase}");

        if (!string.IsNullOrEmpty(KeyPrefix))
            parts.Add($"serviceName={KeyPrefix}");

        return string.Join(",", parts);
    }
}
