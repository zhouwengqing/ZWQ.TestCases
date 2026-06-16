# WyInfo.RabbitMQ

.NET 10 生产级 RabbitMQ 扩展库，源自真实电商后台项目。

## 核心特性

- **泛型消费者基类** — `BaseMessageConsumer<T>` 封装连接管理、消息消费、重试、幂等、死信路由、连接恢复、健康检查。子类只需实现两个方法。
- **先占位后处理幂等** — 利用数据库唯一索引做分布式互斥锁，INSERT 占位记录（Status=0）后再执行业务，彻底消除 check-then-act 竞态条件。
- **自动连接恢复** — 启动重试（容忍 RabbitMQ 未就绪）+ `AutomaticRecoveryEnabled` TCP 重连 + 事件驱动消费者重建 + 30 秒健康检查兜底。
- **瞬态错误检测** — 区分数据库/网络瞬态故障和业务逻辑错误，瞬态错误自动重新入队而非进死信，数据库恢复后消息自动被处理。
- **死信队列** — 每个消费者独立 DLX 队列，配合 `DeadLetterConsumerService` 记录日志、等待人工介入。
- **一行注册** — `services.AddWyInfoRabbitMq<YourDbContext>()` 完成所有基础设施注册。

## 架构

```
发布者                          消费者（BackgroundService）
  │                                    │
  │ Publish(msg, exchange, key)        │
  ▼                                    │
RabbitMqPublisher ──▶ Exchange ──▶ Queue ──▶ BaseMessageConsumer<T>
                     (Topic)     (持久化)        │
                                                 ├─ TryClaim（INSERT 占位 Status=0）
                                                 ├─ ProcessMessage（带重试）
                                                 ├─ Complete（UPDATE Status=1）
                                                 └─ 失败 → DLX → DeadLetterConsumer
```

## 快速开始

### 1. 引用项目

```bash
dotnet add reference src/WyInfo.RabbitMQ/WyInfo.RabbitMQ.csproj
```

### 2. 配置 DbContext

将 `MqProcessedMessage` 加入你的 DbContext：

```csharp
public class AppDbContext : DbContext
{
    public DbSet<MqProcessedMessage> MqProcessedMessage { get; set; }

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
```

### 3. 注册服务

```csharp
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddWyInfoRabbitMq<AppDbContext>();
builder.Services.AddHostedService<OrderConsumerService>();
builder.Services.AddHostedService<DeadLetterConsumerService>();
```

### 4. 创建消费者

```csharp
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
        // 你的业务逻辑
    }

    protected override string GetMessageId(OrderSubmittedEvent message)
        => message.OrderId.ToString();
}
```

### 5. 发布消息

```csharp
app.MapPost("/orders", (IMessagePublisher publisher, CreateOrderDto dto) =>
{
    publisher.Publish(new OrderSubmittedEvent { ... },
        exchangeName: "order_exchange", routingKey: "order.created");
    return Results.Ok();
});
```

### 6. appsettings.json

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ExchangeName": "order_exchange",
    "DeadLetterExchangeName": "order_dlx_exchange",
    "DeadLetterQueueName": "order_dlx_queue",
    "DeadLetterRoutingKey": "order.created",
    "MaxRetryCount": 3,
    "ConnectionRetryCount": 5,
    "ConnectionRetryIntervalSeconds": 5,
    "RecoveryDelaySeconds": 5
  }
}
```

## 幂等机制：先占位后处理

传统的 check-then-act（先查后写）存在竞态窗口：SELECT 和 INSERT 之间如果消息被重复投递，会导致业务逻辑执行两次。

本库采用 INSERT-first + 唯一索引互斥：

```
1. TryClaimAsync → INSERT 占位记录（Status=0）
   ├─ 成功 → 执行业务逻辑
   └─ 唯一索引冲突 → 跳过（已被其他消费者处理）
2. ProcessMessageAsync → 执行业务逻辑（带重试）
3. CompleteAsync → UPDATE 为 Status=1（成功）或 Status=2（失败）
```

如果消费者在步骤 1 和 3 之间崩溃，30 分钟超时清理机制会自动清除卡住的占位记录，允许重新抢占。

## 项目结构

```
src/WyInfo.RabbitMQ/
├── Connection/
│   └── RabbitMqConnectionManager.cs    # 单例 TCP 连接 + 启动重试
├── Consuming/
│   ├── BaseMessageConsumer.cs          # 泛型消费者基类
│   ├── DeadLetterConsumerService.cs    # 死信队列消费者
│   └── QueueBindingConfiguration.cs    # 队列拓扑配置
├── Idempotency/
│   ├── IMessageIdempotencyStore.cs     # 幂等接口
│   ├── EfMessageIdempotencyStore.cs    # EF Core 实现（泛型 DbContext）
│   └── MqProcessedMessage.cs           # 数据库实体
├── Options/
│   └── RabbitMqOptions.cs             # 配置 POCO
├── Publishing/
│   ├── IMessagePublisher.cs            # 发布者接口
│   └── RabbitMqPublisher.cs           # 发布者实现
└── ServiceCollectionExtensions.cs      # DI 注册扩展方法

samples/WyInfo.RabbitMQ.Sample/
└── Program.cs                          # 完整可运行示例
```

## 环境要求

- .NET 10
- RabbitMQ 3.x（建议开启 management 插件）
- Entity Framework Core 10

## 许可证

MIT
