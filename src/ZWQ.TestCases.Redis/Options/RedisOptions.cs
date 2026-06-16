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

    // ---------- 缓存安全防护 ----------

    /// <summary>
    /// 【雪崩防护】TTL 随机抖动百分比（0~50），设为 10 表示实际过期时间在 ±10% 范围浮动
    /// 避免大量 key 同时过期导致数据库瞬时压力
    /// </summary>
    public int ExpirationJitterPercent { get; set; } = 10;

    /// <summary>
    /// 【穿透防护】是否缓存空值（工厂方法返回 null 时写入占位记录）
    /// </summary>
    public bool CacheNullValues { get; set; } = true;

    /// <summary>
    /// 【穿透防护】空值缓存过期时间（分钟），应远小于正常缓存
    /// </summary>
    public int NullValueExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// 【击穿防护】GetOrSet 缓存未命中时是否启用分布式锁（防止并发穿透到数据库）
    /// </summary>
    public bool EnableBreakdownLock { get; set; } = true;

    /// <summary>
    /// 【击穿防护】等待锁的超时（毫秒）
    /// </summary>
    public int BreakdownLockTimeoutMs { get; set; } = 3000;

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

        // KeyPrefix 在应用层使用（RedisCacheService.PrefixKey），不放入连接字符串

        return string.Join(",", parts);
    }
}
