using ZWQ.TestCases.VectorSearch.Models;

namespace ZWQ.TestCases.VectorSearch.Qdrant;

/// <summary>
/// Qdrant 集合管理器接口
/// </summary>
public interface IQdrantCollectionManager
{
    /// <summary>确保集合存在（不存在则创建）</summary>
    Task EnsureCollectionAsync(CancellationToken ct = default);

    /// <summary>批量 Upsert 向量点</summary>
    Task UpsertBatchAsync(IReadOnlyList<(Guid PointId, float[] Vector, ImageDocument Payload)> points, CancellationToken ct = default);

    /// <summary>向量搜索</summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(float[] queryVector, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default);

    /// <summary>根据图片路径计算确定性的 Point ID（SHA256 → Guid）</summary>
    Guid ComputePointId(string imagePath);
}
