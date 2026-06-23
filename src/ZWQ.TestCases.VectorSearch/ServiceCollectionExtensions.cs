using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Indexing;
using ZWQ.TestCases.VectorSearch.Options;
using ZWQ.TestCases.VectorSearch.Qdrant;
using ZWQ.TestCases.VectorSearch.Search;

namespace ZWQ.TestCases.VectorSearch;

/// <summary>
/// VectorSearch 模块 DI 注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 ZWQ VectorSearch 模块的所有服务（Qdrant + CLIP ONNX + 索引 + 搜索）
    /// </summary>
    public static IServiceCollection AddZwqVectorSearch(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<VectorSearchOptions>(configuration.GetSection("VectorSearch"));
        services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
        services.Configure<ClipModelOptions>(configuration.GetSection("ClipModel"));

        // Embeddings - 单例（持有昂贵的 ONNX Session 资源）
        services.AddSingleton<ImagePreprocessor>();
        services.AddSingleton<BpeTokenizer>();
        services.AddSingleton<IClipEmbeddingService, ClipEmbeddingService>();

        // Qdrant 客户端 + 集合管理器
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
            return new QdrantClient(
                host: opts.Host,
                port: opts.GrpcPort,
                https: opts.UseHttps,
                apiKey: opts.ApiKey);
        });
        services.AddSingleton<IQdrantCollectionManager, QdrantCollectionManager>();

        // 索引服务
        services.AddSingleton<IVectorIndexService, VectorIndexService>();
        services.AddHostedService<ImageIndexingBackgroundService>();

        // 搜索服务
        services.AddSingleton<IVectorSearchService, VectorSearchService>();

        return services;
    }
}
