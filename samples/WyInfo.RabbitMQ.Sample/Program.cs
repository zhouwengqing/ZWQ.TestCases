using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WyInfo.RabbitMQ;
using WyInfo.RabbitMQ.Connection;
using WyInfo.RabbitMQ.Consuming;
using WyInfo.RabbitMQ.Idempotency;
using WyInfo.RabbitMQ.Options;
using WyInfo.RabbitMQ.Publishing;

var builder = WebApplication.CreateBuilder(args);

// 1. 配置 RabbitMQ 选项（从 appsettings.json 绑定）
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

// 2. 一键注册 RabbitMQ 基础设施
builder.Services.AddWyInfoRabbitMq<SampleDbContext>();

// 3. 注册消费者（BackgroundService）
builder.Services.AddHostedService<OrderConsumerService>();
builder.Services.AddHostedService<DeadLetterConsumerService>();

// 4. 注册 DbContext（示例用 SQLite，实际项目用 SQL Server）
builder.Services.AddDbContext<SampleDbContext>(opt =>
    opt.UseSqlite("Data Source=sample.db"));

var app = builder.Build();

// 确保数据库已创建（含 MqProcessedMessage 表）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/", () => "WyInfo.RabbitMQ Sample is running!");

// 示例：发布消息
app.MapPost("/publish/order", (IMessagePublisher publisher) =>
{
    publisher.Publish(new OrderSubmittedEvent
    {
        OrderId = Guid.NewGuid(),
        CustomerEmail = "test@example.com",
        Amount = 99.9m,
        Timestamp = DateTime.UtcNow
    }, exchangeName: "order_exchange", routingKey: "order.created");

    return Results.Ok("Order message published!");
});

app.Run();

// ====== 示例 DbContext ======
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

// ====== 示例消息类型 ======
public class OrderSubmittedEvent
{
    public Guid OrderId { get; set; }
    public DateTime Timestamp { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// ====== 示例消费者 ======
public class OrderConsumerService : BaseMessageConsumer<OrderSubmittedEvent>
{
    public OrderConsumerService(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<OrderConsumerService> logger,
        IServiceScopeFactory scopeFactory)
        : base(connectionManager,
            new QueueBindingConfiguration
            {
                ExchangeName = "order_exchange",
                QueueName = "order_queue",
                RoutingKey = "order.created",
                DeadLetterExchangeName = options.Value.DeadLetterExchangeName,
                DeadLetterQueueName = "order_dlx_queue",
                DeadLetterRoutingKey = "order.created",
                MaxRetryCount = options.Value.MaxRetryCount
            },
            logger, scopeFactory, options) { }

    protected override async Task ProcessMessageAsync(OrderSubmittedEvent message)
    {
        _logger.LogInformation("Processing order {OrderId}, Amount: {Amount}", message.OrderId, message.Amount);
        await Task.Delay(1000); // 模拟业务处理
    }

    protected override string GetMessageId(OrderSubmittedEvent message) => message.OrderId.ToString();
}
