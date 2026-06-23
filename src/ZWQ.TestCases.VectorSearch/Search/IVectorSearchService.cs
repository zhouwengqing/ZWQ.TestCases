using ZWQ.TestCases.VectorSearch.Models;

namespace ZWQ.TestCases.VectorSearch.Search;

/// <summary>
/// 向量搜索服务接口
/// </summary>
public interface IVectorSearchService
{
    /// <summary>文字搜图 - 用自然语言搜索相似图片</summary>
    Task<IReadOnlyList<SearchResult>> TextToImageSearchAsync(string query, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default);

    /// <summary>以图搜图 - 用图片搜索视觉相似的图片</summary>
    Task<IReadOnlyList<SearchResult>> ImageToImageSearchAsync(string imagePath, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default);
}
