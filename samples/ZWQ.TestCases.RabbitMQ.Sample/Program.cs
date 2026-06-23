using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using ZWQ.TestCases.RabbitMQ;
using ZWQ.TestCases.RabbitMQ.Consuming;
using ZWQ.TestCases.RabbitMQ.Sample.Consumers;
using ZWQ.TestCases.RabbitMQ.Sample.Data;
using ZWQ.TestCases.RabbitMQ.Sample.Services;
using ZWQ.TestCases.Redis;
using ZWQ.TestCases.DesignPatterns;
using ZWQ.TestCases.VectorSearch;

var builder = WebApplication.CreateBuilder(args);

// ====== 1. RabbitMQ 基础设施 ======
builder.Services.Configure<ZWQ.TestCases.RabbitMQ.Options.RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddZwqTestCasesRabbitMq<SampleDbContext>();

// ====== 2. Redis 基础设施 ======
builder.Services.Configure<ZWQ.TestCases.Redis.Options.RedisOptions>(
    builder.Configuration.GetSection("Redis"));
builder.Services.AddZwqRedis();

// ====== 3. 设计模式（策略模式 + 工厂模式） ======
builder.Services.AddZwqDesignPatterns();

// ====== 4. 向量搜索（Qdrant + CLIP ONNX） ======
builder.Services.AddZwqVectorSearch(builder.Configuration);

// ====== 5. 消费者 ======
builder.Services.AddHostedService<OrderConsumerService>();
builder.Services.AddHostedService<PaymentConsumerService>();
builder.Services.AddHostedService<NotificationConsumerService>();
builder.Services.AddHostedService<DeadLetterConsumerService>();

// ====== 6. DbContext ======
builder.Services.AddDbContext<SampleDbContext>(opt =>
    opt.UseSqlite("Data Source=sample.db"));

// ====== 7. 业务 Service ======
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ====== 8. Controller + Swagger ======
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // 枚举序列化为字符串，方便前端使用 "Alipay" 而非数字 0
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ZWQ.TestCases API",
        Version = "v1",
        Description = "ZWQ 测试用例集合 — RabbitMQ 消息队列 / Redis 缓存 / 设计模式（策略+工厂） / 向量搜索（Qdrant+CLIP）测试接口"
    });

    // 加载 XML 注释文件
    var basePath = AppContext.BaseDirectory;
    foreach (var xml in Directory.GetFiles(basePath, "*.xml"))
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);
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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZWQ.TestCases v1");
    c.RoutePrefix = string.Empty; // 根路径打开 Swagger
});

app.MapControllers();

app.Run();
