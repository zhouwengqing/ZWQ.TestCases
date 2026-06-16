using Microsoft.EntityFrameworkCore;
using ZWQ.TestCases.RabbitMQ.Idempotency;

namespace ZWQ.TestCases.RabbitMQ.Sample.Data;

/// <summary>
/// 示例项目 DbContext — 包含 MQ 幂等记录表
/// </summary>
public class SampleDbContext : DbContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">DbContext 配置选项</param>
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    /// <summary>
    /// MQ 消息幂等记录表 — 用于消息去重和幂等校验
    /// </summary>
    public DbSet<MqProcessedMessage> MqProcessedMessage { get; set; } = null!;

    /// <summary>
    /// 配置实体映射和索引
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MqProcessedMessage>(b =>
        {
            b.ToTable("MqProcessedMessage");
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.MessageId, p.QueueName }).IsUnique();
            b.HasIndex(p => p.ExpireAt);
        });
    }
}
