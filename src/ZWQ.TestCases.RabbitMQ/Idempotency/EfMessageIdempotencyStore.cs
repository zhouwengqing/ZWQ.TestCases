using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ZWQ.TestCases.RabbitMQ.Idempotency;

/// <summary>
/// 基于 EF Core 的消息幂等存储实现
/// 采用"先占位，再处理"模式，利用唯一索引防止并发处理
/// 
/// 使用方式：你的 DbContext 需要包含 DbSet&lt;MqProcessedMessage&gt; 并在 OnModelCreating 中配置唯一索引
/// </summary>
public class EfMessageIdempotencyStore<TDbContext> : IMessageIdempotencyStore
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private const int ProcessingTimeoutMinutes = 30;

    public EfMessageIdempotencyStore(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<MqProcessedMessage> Messages => _dbContext.Set<MqProcessedMessage>();

    public async Task<bool> IsProcessedAsync(string messageId, string queueName)
    {
        return await Messages.AsNoTracking()
            .AnyAsync(x => x.MessageId == messageId && x.QueueName == queueName);
    }

    public async Task<bool> TryClaimAsync(string messageId, string queueName, string? messageType = null, string? messageBody = null)
    {
        try
        {
            await CleanupStaleProcessingRecordsAsync(messageId, queueName);

            var record = new MqProcessedMessage
            {
                MessageId = messageId,
                QueueName = queueName,
                MessageType = messageType,
                MessageBody = Truncate(messageBody, 2000),
                Status = 0,
                ProcessedAt = DateTime.Now,
                ExpireAt = DateTime.Now.AddHours(1)
            };

            Messages.Add(record);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries<MqProcessedMessage>())
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
            }
            return false;
        }
    }

    public async Task CompleteAsync(string messageId, string queueName, int status,
        string? messageType = null, string? messageBody = null, long? elapsedMs = null)
    {
        var record = await Messages
            .FirstOrDefaultAsync(x => x.MessageId == messageId && x.QueueName == queueName);

        if (record == null) return;

        record.Status = status;
        record.MessageType = messageType ?? record.MessageType;
        record.MessageBody = messageBody ?? record.MessageBody;
        record.ElapsedMs = elapsedMs;
        record.ProcessedAt = DateTime.Now;
        record.ExpireAt = DateTime.Now.AddDays(status == 1 ? 7 : 30);

        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkAsProcessedAsync(string messageId, string queueName,
        string? messageType = null, string? messageBody = null, long? elapsedMs = null)
    {
        Messages.Add(new MqProcessedMessage
        {
            MessageId = messageId, QueueName = queueName,
            MessageType = messageType, MessageBody = Truncate(messageBody, 2000),
            Status = 1, ProcessedAt = DateTime.Now,
            ElapsedMs = elapsedMs, ExpireAt = DateTime.Now.AddDays(7)
        });
        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkAsFailedAsync(string messageId, string queueName,
        string? messageType = null, string? messageBody = null)
    {
        Messages.Add(new MqProcessedMessage
        {
            MessageId = messageId, QueueName = queueName,
            MessageType = messageType, MessageBody = Truncate(messageBody, 2000),
            Status = 2, ProcessedAt = DateTime.Now,
            ExpireAt = DateTime.Now.AddDays(30)
        });
        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> CleanExpiredAsync()
    {
        var expired = await Messages.Where(x => x.ExpireAt < DateTime.Now).ToListAsync();
        if (expired.Count > 0)
        {
            Messages.RemoveRange(expired);
            await _dbContext.SaveChangesAsync();
        }
        return expired.Count;
    }

    private async Task CleanupStaleProcessingRecordsAsync(string messageId, string queueName)
    {
        var stale = await Messages.FirstOrDefaultAsync(x =>
            x.MessageId == messageId && x.QueueName == queueName &&
            x.Status == 0 && x.ProcessedAt < DateTime.Now.AddMinutes(-ProcessingTimeoutMinutes));

        if (stale != null)
        {
            Messages.Remove(stale);
            await _dbContext.SaveChangesAsync();
        }
    }

    private static string? Truncate(string? value, int maxLength)
        => value?.Length > maxLength ? value[..maxLength] : value;
}
