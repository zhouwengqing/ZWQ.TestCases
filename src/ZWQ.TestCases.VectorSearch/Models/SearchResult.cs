namespace ZWQ.TestCases.VectorSearch.Models;

/// <summary>
/// 向量搜索结果
/// </summary>
public sealed class SearchResult
{
    /// <summary>Qdrant 中的 Point ID</summary>
    public Guid PointId { get; init; }

    /// <summary>余弦相似度分数（0~1）</summary>
    public float Score { get; init; }

    /// <summary>匹配的图片文档信息</summary>
    public required ImageDocument Document { get; init; }
}
