using Microsoft.EntityFrameworkCore;
using WyInfo.RabbitMQ.Idempotency;

namespace WyInfo.RabbitMQ.Sample.Data;

public class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    public DbSet<MqProcessedMessage> MqProcessedMessage { get; set; } = null!;

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
