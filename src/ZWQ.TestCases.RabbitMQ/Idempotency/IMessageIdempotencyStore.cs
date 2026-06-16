using System.Threading.Tasks;

namespace ZWQ.TestCases.RabbitMQ.Idempotency;

/// <summary>
/// 消息幂等存储接口
/// 采用"先占位，再处理"模式：
///   1. TryClaimAsync → INSERT 占位记录（Status=0），利用唯一索引实现互斥
///   2. 执行业务逻辑
///   3. CompleteAsync → UPDATE 为最终状态（Status=1 或 Status=2）
/// </summary>
public interface IMessageIdempotencyStore
{
    /// <summary>检查消息是否已被处理（包括处理中）</summary>
    Task<bool> IsProcessedAsync(string messageId, string queueName);

    /// <summary>
    /// 尝试抢占消息处理权（INSERT 占位记录 Status=0）
    /// 利用 (MessageId, QueueName) 唯一索引实现互斥
    /// </summary>
    Task<bool> TryClaimAsync(string messageId, string queueName, string? messageType = null, string? messageBody = null);

    /// <summary>完成处理，更新占位记录为最终状态</summary>
    Task CompleteAsync(string messageId, string queueName, int status, string? messageType = null, string? messageBody = null, long? elapsedMs = null);

    /// <summary>标记消息为已处理成功</summary>
    Task MarkAsProcessedAsync(string messageId, string queueName, string? messageType = null, string? messageBody = null, long? elapsedMs = null);

    /// <summary>标记消息处理失败</summary>
    Task MarkAsFailedAsync(string messageId, string queueName, string? messageType = null, string? messageBody = null);

    /// <summary>清理过期的幂等记录</summary>
    Task<int> CleanExpiredAsync();
}
