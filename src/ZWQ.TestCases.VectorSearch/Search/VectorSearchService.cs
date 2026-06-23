using Microsoft.Extensions.Logging;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Models;
using ZWQ.TestCases.VectorSearch.Qdrant;

namespace ZWQ.TestCases.VectorSearch.Search;

/// <summary>
/// 向量搜索服务 - 通过 CLIP 生成查询向量并在 Qdrant 中检索
/// </summary>
public sealed class VectorSearchService : IVectorSearchService
{
    private readonly IClipEmbeddingService _embeddingService;
    private readonly IQdrantCollectionManager _qdrant;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        IClipEmbeddingService embeddingService,
        IQdrantCollectionManager qdrant,
        ILogger<VectorSearchService> logger)
    {
        _embeddingService = embeddingService;
        _qdrant = qdrant;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResult>> TextToImageSearchAsync(
        string query, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("[搜索] 收到空的文本查询");
            return Array.Empty<SearchResult>();
        }

        _logger.LogInformation("[搜索] 以文搜图: '{Query}', topK={TopK}", query, topK);
        float[] embedding = await _embeddingService.GetTextEmbeddingAsync(query, ct);
        return await _qdrant.SearchAsync(embedding, topK, scoreThreshold, ct);
    }

    public async Task<IReadOnlyList<SearchResult>> ImageToImageSearchAsync(
        string imagePath, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Image not found for similarity search: {fullPath}");

        _logger.LogInformation("[搜索] 以图搜图: '{Path}', topK={TopK}", fullPath, topK);
        float[] embedding = await _embeddingService.GetImageEmbeddingAsync(fullPath, ct);
        return await _qdrant.SearchAsync(embedding, topK, scoreThreshold, ct);
    }
}
