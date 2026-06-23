namespace ZWQ.TestCases.VectorSearch.Models;

/// <summary>
/// 增量索引请求体
/// </summary>
public sealed class IndexingRequest
{
    /// <summary>
    /// 需要索引的图片路径列表
    /// </summary>
    public required IReadOnlyList<string> ImagePaths { get; init; }
}
