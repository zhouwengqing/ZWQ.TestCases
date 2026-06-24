# ============================================================
# ZWQ.TestCases — 多阶段 Docker 构建
# ============================================================

# ── Stage 1: 构建 ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先复制 csproj 和 sln，利用 Docker 缓存层加速 NuGet 还原
COPY ZWQ.TestCases.sln .
COPY src/ZWQ.TestCases.RabbitMQ/ZWQ.TestCases.RabbitMQ.csproj src/ZWQ.TestCases.RabbitMQ/
COPY src/ZWQ.TestCases.Redis/ZWQ.TestCases.Redis.csproj src/ZWQ.TestCases.Redis/
COPY src/ZWQ.TestCases.DesignPatterns/ZWQ.TestCases.DesignPatterns.csproj src/ZWQ.TestCases.DesignPatterns/
COPY src/ZWQ.TestCases.VectorSearch/ZWQ.TestCases.VectorSearch.csproj src/ZWQ.TestCases.VectorSearch/
COPY src/ZWQ.TestCases.TextSearch/ZWQ.TestCases.TextSearch.csproj src/ZWQ.TestCases.TextSearch/
COPY samples/ZWQ.TestCases.RabbitMQ.Sample/ZWQ.TestCases.RabbitMQ.Sample.csproj samples/ZWQ.TestCases.RabbitMQ.Sample/

RUN dotnet restore samples/ZWQ.TestCases.RabbitMQ.Sample/ZWQ.TestCases.RabbitMQ.Sample.csproj

# 复制全部源码并构建
COPY src/ src/
COPY samples/ samples/
RUN dotnet publish samples/ZWQ.TestCases.RabbitMQ.Sample/ZWQ.TestCases.RabbitMQ.Sample.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: 运行 ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# 从构建阶段复制发布产物
COPY --from=build /app/publish .

# 创建数据目录（SQLite、用户词典等）
RUN mkdir -p /data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ZWQ.TestCases.RabbitMQ.Sample.dll"]
