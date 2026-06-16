# WyInfo.RabbitMQ

A production-ready RabbitMQ extension library for .NET 10, built from real-world e-commerce backend experience.

## Features

- **Generic Consumer Base Class** — `BaseMessageConsumer<T>` encapsulates connection management, message consumption, retry, idempotency, dead letter routing, connection recovery, and health checks. Subclasses only implement two methods.
- **Claim-then-Process Idempotency** — Uses database unique index as a distributed mutex lock. INSERT a claim record (Status=0) before processing, UPDATE to final status after. Eliminates the check-then-act race condition.
- **Automatic Connection Recovery** — Startup retry (survives RabbitMQ not being ready), `AutomaticRecoveryEnabled` for TCP reconnection, event-driven consumer rebuild, and 30-second health check loop as a fallback.
- **Transient Error Detection** — Distinguishes database/network transient errors from business logic errors. Transient errors trigger requeue instead of dead letter, so messages auto-retry when the database recovers.
- **Dead Letter Queue** — Per-consumer DLX queues with a shared `DeadLetterConsumerService` for logging and alerting.
- **One-line DI Registration** — `services.AddWyInfoRabbitMq<YourDbContext>()` registers all infrastructure.

## Architecture

```
Publisher                        Consumer (BackgroundService)
   │                                   │
   │ Publish(msg, exchange, key)       │
   ▼                                   │
RabbitMqPublisher ──▶ Exchange ──▶ Queue ──▶ BaseMessageConsumer<T>
                      (Topic)     (durable)        │
                                                   ├─ TryClaim (INSERT Status=0)
                                                   ├─ ProcessMessage (with retry)
                                                   ├─ Complete (UPDATE Status=1)
                                                   └─ On failure → DLX → DeadLetterConsumer
```

## Quick Start

### 1. Install

```bash
dotnet add reference src/WyInfo.RabbitMQ/WyInfo.RabbitMQ.csproj
```

### 2. Configure DbContext

Add `MqProcessedMessage` to your DbContext:

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

### 3. Register Services

```csharp
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddWyInfoRabbitMq<AppDbContext>();
builder.Services.AddHostedService<OrderConsumerService>();
builder.Services.AddHostedService<DeadLetterConsumerService>();
```

### 4. Create a Consumer

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
        // Your business logic here
    }

    protected override string GetMessageId(OrderSubmittedEvent message)
        => message.OrderId.ToString();
}
```

### 5. Publish Messages

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

## Idempotency: Claim-then-Process

Traditional check-then-act has a race condition window between SELECT and INSERT. This library uses INSERT-first with a unique index constraint:

```
1. TryClaimAsync → INSERT record (Status=0)
   ├─ Success → proceed to business logic
   └─ Unique constraint violation → skip (already claimed)
2. ProcessMessageAsync → execute business logic
3. CompleteAsync → UPDATE to Status=1 (success) or Status=2 (failure)
```

A 30-minute timeout cleanup prevents stale claim records from blocking reprocessing if a consumer crashes between step 1 and step 3.

## Project Structure

```
src/WyInfo.RabbitMQ/
├── Connection/
│   └── RabbitMqConnectionManager.cs    # Singleton TCP connection with retry
├── Consuming/
│   ├── BaseMessageConsumer.cs          # Generic consumer base class
│   ├── DeadLetterConsumerService.cs    # Dead letter queue consumer
│   └── QueueBindingConfiguration.cs    # Per-consumer topology config
├── Idempotency/
│   ├── IMessageIdempotencyStore.cs     # Idempotency interface
│   ├── EfMessageIdempotencyStore.cs    # EF Core implementation
│   └── MqProcessedMessage.cs           # Database entity
├── Options/
│   └── RabbitMqOptions.cs             # Configuration POCO
├── Publishing/
│   ├── IMessagePublisher.cs            # Publisher interface
│   └── RabbitMqPublisher.cs           # Publisher implementation
└── ServiceCollectionExtensions.cs      # DI registration

samples/WyInfo.RabbitMQ.Sample/
└── Program.cs                          # Complete working example
```

## Requirements

- .NET 10
- RabbitMQ 3.x with management plugin
- Entity Framework Core 10

## License

MIT
