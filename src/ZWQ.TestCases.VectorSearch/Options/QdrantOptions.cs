namespace ZWQ.TestCases.VectorSearch.Options;

/// <summary>
/// Qdrant 向量数据库连接配置
/// </summary>
public sealed class QdrantOptions
{
    /// <summary>
    /// Qdrant 服务地址
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// gRPC 端口（默认 6334）
    /// </summary>
    public int GrpcPort { get; set; } = 6334;

    /// <summary>
    /// HTTP REST 端口（默认 6333）
    /// </summary>
    public int HttpPort { get; set; } = 6333;

    /// <summary>
    /// 是否使用 HTTPS/TLS
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// 集合名称
    /// </summary>
    public string CollectionName { get; set; } = "images";

    /// <summary>
    /// API Key（可选）
    /// </summary>
    public string? ApiKey { get; set; }
}
