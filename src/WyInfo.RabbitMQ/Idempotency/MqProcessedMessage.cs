using System;
using System.ComponentModel.DataAnnotations;

namespace WyInfo.RabbitMQ.Idempotency;

/// <summary>
/// MQ 消息幂等记录实体
/// 需要在 DbContext 中配置 (MessageId, QueueName) 唯一索引
/// </summary>
public class MqProcessedMessage
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string QueueName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? MessageType { get; set; }

    /// <summary>处理状态：0=处理中（占位） 1=已处理 2=处理失败</summary>
    public int Status { get; set; } = 0;

    public DateTime ProcessedAt { get; set; }

    [MaxLength(2000)]
    public string? MessageBody { get; set; }

    public long? ElapsedMs { get; set; }

    public DateTime ExpireAt { get; set; }
}
