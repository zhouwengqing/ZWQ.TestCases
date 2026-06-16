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
│       ├── Caching/                      # 分布式缓存服务（含穿透/击穿/雪崩防护）
│       ├── BloomFilter/                  # 布隆过滤器（穿透防护第一线）
│       ├── Locking/                      # 分布式锁
│       ├── Monitoring/                   # 心跳监控（BackgroundService）
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
- **缓存穿透防护** — 工厂返回 null 时缓存 `__NULL__` 占位标记（短 TTL），阻止恶意/异常请求穿透到数据库
- **缓存击穿防护** — `GetOrSetAsync` 缓存未命中时自动抢分布式锁（SET NX），仅一个线程回源查库，其余线程等待后重读缓存
- **缓存雪崩防护** — 写入缓存时 TTL 自动加上随机抖动（默认 ±10%），避免大批 key 同时过期
- **布隆过滤器** — `IBloomFilter` 基于 Redis BitArray + 双重哈希，O(K) 判断元素是否存在，作为穿透防护的第一道防线
- **心跳监控** — `RedisHealthMonitor` 定时 PING 检测，记录延迟/成功率/连续失败，状态变化自动告警
- **一行注册** — `services.AddZwqRedis()`

## 快速开始

### 1. 安装 Redis

Windows 推荐通过 [Memurai](https://www.memurai.com/) 或 Docker 安装：

```bash
docker run -d --name redis -p 6379:6379 redis:5
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
    "DefaultExpirationMinutes": 30,
    "ExpirationJitterPercent": 10,
    "CacheNullValues": true,
    "NullValueExpirationMinutes": 5,
    "EnableBreakdownLock": true
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
| Redis | `GET /api/redis/getorset/{key}` | 不存在则加载并缓存（含三重防护） |
| Redis | `POST /api/redis/counter/{key}` | 原子计数器 |
| Redis | `POST /api/redis/lock/{key}` | 分布式锁测试 |
| Bloom | `POST /api/bloom/init/{key}` | 初始化布隆过滤器 |
| Bloom | `POST /api/bloom/add/{key}` | 添加元素 |
| Bloom | `GET /api/bloom/check/{key}/{item}` | 检查元素是否存在 |
| Bloom | `GET /api/bloom/info/{key}` | 过滤器统计信息 |
| Monitor | `GET /api/redis-monitor/status` | 健康状态总览 |
| Monitor | `GET /api/redis-monitor/history` | 最近心跳记录 |
| Monitor | `GET /api/redis-monitor/health` | 简易健康探针 |

## Redis 缓存三重防护

### 穿透防护（Cache Penetration）

请求的数据在缓存和数据库中都不存在，每次请求都打到数据库。

解决方案：`GetOrSetAsync` 中工厂方法返回 `null` 时，自动缓存 `__NULL__` 占位标记（默认 5 分钟过期）。后续相同 key 的请求直接命中占位返回 null，不再查库。

```
请求 → GetOrSetAsync("user:99999")
  ├─ 缓存命中 __NULL__ → 直接返回 null（不打库）
  ├─ 缓存未命中 → 查库 → 数据库也没有 → 写入 __NULL__（5 分钟）
  └─ 缓存未命中 → 查库 → 数据库有 → 正常缓存数据
```

配置项：`CacheNullValues`（开关）、`NullValueExpirationMinutes`（占位过期时间）。

### 击穿防护（Cache Breakdown）

某个热点 key 缓存过期的瞬间，大量并发请求同时打到数据库。

解决方案：`GetOrSetAsync` 缓存未命中时，使用 `SET NX` 抢分布式互斥锁，只有抢到锁的线程执行工厂方法回源，其余线程等待后重读缓存。

```
请求A → 缓存未命中 → 抢到锁 → 查库 → 写入缓存 → 释放锁
请求B → 缓存未命中 → 抢锁失败 → 等待 3 秒 → 重读缓存 → 命中
请求C → 缓存未命中 → 抢锁失败 → 等待 3 秒 → 重读缓存 → 命中
```

配置项：`EnableBreakdownLock`（开关）、`BreakdownLockTimeoutMs`（等待超时）。

### 雪崩防护（Cache Avalanche）

大批 key 同时过期，或 Redis 宕机，导致请求全部打到数据库。

解决方案：所有 `Set` 操作写入缓存时，TTL 自动加上随机抖动（默认 ±10%），使过期时间分散在一个范围内。

```
key1: TTL = 30 分钟 → 实际 27~33 分钟
key2: TTL = 30 分钟 → 实际 28.5~31.5 分钟
key3: TTL = 30 分钟 → 实际 30.2~33 分钟
```

配置项：`ExpirationJitterPercent`（抖动百分比，0~50）。

## 布隆过滤器（穿透防护第一线）

在缓存穿透防护中，空值缓存是第二道防线。第一道防线是**布隆过滤器**：在查缓存之前先判断 key 是否合法，直接拦截掉一定不存在的请求。

```
请求 → 布隆过滤器检查
  ├─ "一定不存在" → 直接拒绝（0 次 DB 查询）
  └─ "可能存在"   → 正常流程：查缓存 → 查库 → 写缓存
```

使用方式：

```csharp
// 1. 初始化（预期 10 万条数据，1% 误判率）
await bloomFilter.InitializeAsync("user_ids", expectedInsertions: 100000, falsePositiveRate: 0.01);

// 2. 批量灌入已有数据（应用启动时从 DB 加载）
await bloomFilter.AddManyAsync("user_ids", existingUserIds);

// 3. 查询前检查
if (!await bloomFilter.ContainsAsync("user_ids", requestedId))
    return null; // 一定不存在，直接返回
```

原理：使用 K 个哈希函数将元素映射到 M 位的 BitArray（Redis SETBIT/GETBIT），判断不存在 100% 准确，判断可能存在有可控误判率。

## Redis 心跳监控

`RedisHealthMonitor` 是一个 `BackgroundService`，应用启动后自动运行：

- 每 15 秒 PING 一次 Redis，记录延迟和成功/失败
- 连接状态变化时自动输出告警日志（断线/恢复）
- 连续失败时降低日志频率（每 10 次打一次），避免日志轰炸
- 通过 `/api/redis-monitor/status` 查看完整健康报告
- 通过 `/api/redis-monitor/health` 作为负载均衡健康探针

## 环境要求

- .NET 10
- RabbitMQ 3.x（建议开启 management 插件）
- Redis 5.x+

## 许可证

MIT
