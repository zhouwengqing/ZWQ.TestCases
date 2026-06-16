# ZWQ.TestCases

.NET 10 中间件测试用例集合，涵盖 RabbitMQ 消息队列、Redis 分布式缓存/锁等场景，每个模块均为生产级实现。

## 项目结构

```
ZWQ.TestCases.sln
├── src/
│   ├── ZWQ.TestCases.RabbitMQ/          # RabbitMQ 扩展库
│   │   ├── Connection/                   # 连接管理器（单例 + 自动恢复）
│   │   ├── Consuming/                    # 泛型消费者基类 + 死信消费者
│   │   ├── Publishing/                   # 消息发布者
│   │   ├── Idempotency/                  # 先占位后处理幂等（EF Core）
│   │   └── Options/                      # 配置 POCO
│   └── ZWQ.TestCases.Redis/              # Redis 扩展库
│       ├── Connection/                   # 连接管理器（单例 + 自动重连）
│       ├── Caching/                      # 分布式缓存服务
│       ├── Locking/                      # 分布式锁
│       └── Options/                      # 配置 POCO
└── samples/
    └── ZWQ.TestCases.RabbitMQ.Sample/    # 可运行测试项目（Swagger UI）
```

## 核心特性

### RabbitMQ 模块

- **泛型消费者基类** — `BaseMessageConsumer<T>` 封装连接管理、消费、重试、幂等、死信、连接恢复、健康检查
- **先占位后处理幂等** — INSERT 占位 + 唯一索引互斥，消除 check-then-act 竞态条件
- **自动连接恢复** — 启动重试 + `AutomaticRecoveryEnabled` + 事件驱动重建 + 30 秒健康检查
- **瞬态错误检测** — 区分数据库/网络瞬态故障和业务错误，瞬态错误自动重新入队
- **一行注册** — `services.AddZwqTestCasesRabbitMq<YourDbContext>()`

### Redis 模块

- **连接管理器** — 单例 `IConnectionMultiplexer`，启动重试 + `ConnectionRestored` 事件自动重连
- **分布式缓存** — `ICacheService` 支持 JSON 序列化、键前缀、GetOrSet、原子计数器
- **分布式锁** — `ILockService` 基于 SET NX EX，Lua 脚本安全释放，支持等待重试
- **一行注册** — `services.AddZwqRedis()`

## 快速开始

### 1. 安装 Redis

Windows 推荐通过 [Memurai](https://www.memurai.com/) 或 Docker 安装：

```bash
docker run -d --name redis -p 6379:6379 redis:7
```

### 2. 安装 RabbitMQ

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 3. 运行示例项目

```bash
cd samples/ZWQ.TestCases.RabbitMQ.Sample
dotnet run
```

启动后访问 `http://localhost:5000` 打开 Swagger UI，即可测试所有接口。

### 4. 配置（appsettings.json）

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "KeyPrefix": "zwq",
    "DefaultExpirationMinutes": 30
  }
}
```

## 测试接口一览

| 模块 | 接口 | 说明 |
|------|------|------|
| RabbitMQ | `POST /api/order` | 创建订单并发布 MQ 消息 |
| RabbitMQ | `POST /api/order/batch` | 批量创建订单（并发测试） |
| RabbitMQ | `POST /api/payment` | 创建支付并发布消息 |
| RabbitMQ | `POST /api/notification` | 发送通知消息 |
| RabbitMQ | `GET /api/mqdiagnostic/*` | 幂等记录查询/统计/清理 |
| Redis | `GET /api/redis/ping` | 连接状态检测 |
| Redis | `POST /api/redis/set` | 设置缓存 |
| Redis | `GET /api/redis/get/{key}` | 获取缓存 |
| Redis | `GET /api/redis/getorset/{key}` | 不存在则加载并缓存 |
| Redis | `POST /api/redis/counter/{key}` | 原子计数器 |
| Redis | `POST /api/redis/lock/{key}` | 分布式锁测试 |

## 环境要求

- .NET 10
- RabbitMQ 3.x（建议开启 management 插件）
- Redis 7.x

## 许可证

MIT
