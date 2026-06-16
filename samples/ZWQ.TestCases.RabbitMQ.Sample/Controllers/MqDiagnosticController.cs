using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZWQ.TestCases.RabbitMQ.Idempotency;
using ZWQ.TestCases.RabbitMQ.Sample.Data;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// MQ 诊断接口 — 查看幂等记录、清理过期数据、手动触发清理
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MqDiagnosticController : ControllerBase
{
    private readonly SampleDbContext _dbContext;
    private readonly IMessageIdempotencyStore _idempotencyStore;

    public MqDiagnosticController(SampleDbContext dbContext, IMessageIdempotencyStore idempotencyStore)
    {
        _dbContext = dbContext;
        _idempotencyStore = idempotencyStore;
    }

    /// <summary>
    /// 查看所有幂等记录（消息处理历史）
    /// </summary>
    [HttpGet("idempotency")]
    public async Task<IActionResult> GetIdempotencyRecords(
        [FromQuery] string? queueName = null,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.MqProcessedMessage.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(queueName))
            query = query.Where(x => x.QueueName == queueName);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync();
        var rawRecords = await query
            .OrderByDescending(x => x.ProcessedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var records = rawRecords.Select(x => new
        {
            x.Id,
            x.MessageId,
            x.QueueName,
            x.MessageType,
            Status = x.Status == 0 ? "处理中" : x.Status == 1 ? "成功" : x.Status == 2 ? "失败" : "未知",
            x.ProcessedAt,
            x.ElapsedMs,
            x.MessageBody,
            x.ExpireAt
        }).ToList();

        return Ok(new { total, page, pageSize, records });
    }

    /// <summary>
    /// 查看各队列消息统计
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _dbContext.MqProcessedMessage.AsNoTracking()
            .GroupBy(x => x.QueueName)
            .Select(g => new
            {
                QueueName = g.Key,
                Total = g.Count(),
                Processing = g.Count(x => x.Status == 0),
                Success = g.Count(x => x.Status == 1),
                Failed = g.Count(x => x.Status == 2),
                LastProcessed = g.Max(x => x.ProcessedAt)
            })
            .ToListAsync();

        return Ok(stats);
    }

    /// <summary>
    /// 手动清理过期的幂等记录
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupExpired()
    {
        var count = await _idempotencyStore.CleanExpiredAsync();
        return Ok(new { message = $"已清理 {count} 条过期记录" });
    }
}
