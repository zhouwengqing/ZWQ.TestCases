using Microsoft.EntityFrameworkCore;
using WyInfo.RabbitMQ;
using WyInfo.RabbitMQ.Consuming;
using WyInfo.RabbitMQ.Sample.Consumers;
using WyInfo.RabbitMQ.Sample.Data;
using WyInfo.RabbitMQ.Sample.Services;

var builder = WebApplication.CreateBuilder(args);

// ====== 1. RabbitMQ 基础设施 ======
builder.Services.Configure<WyInfo.RabbitMQ.Options.RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddWyInfoRabbitMq<SampleDbContext>();

// ====== 2. 消费者 ======
builder.Services.AddHostedService<OrderConsumerService>();
builder.Services.AddHostedService<PaymentConsumerService>();
builder.Services.AddHostedService<NotificationConsumerService>();
builder.Services.AddHostedService<DeadLetterConsumerService>();

// ====== 3. DbContext ======
builder.Services.AddDbContext<SampleDbContext>(opt =>
    opt.UseSqlite("Data Source=sample.db"));

// ====== 4. 业务 Service ======
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ====== 5. Controller + Swagger ======
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "WyInfo.RabbitMQ Sample API",
        Version = "v1",
        Description = "WyInfo.RabbitMQ 示例项目 — 订单/支付/通知消息发布与消费测试接口"
    });
});

var app = builder.Build();

// 确保数据库已创建
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WyInfo.RabbitMQ Sample v1");
    c.RoutePrefix = string.Empty; // 根路径打开 Swagger
});

app.MapControllers();

app.Run();
