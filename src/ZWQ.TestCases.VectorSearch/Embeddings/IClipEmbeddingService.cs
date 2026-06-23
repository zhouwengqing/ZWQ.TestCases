using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZWQ.TestCases.VectorSearch.Options;

namespace ZWQ.TestCases.VectorSearch.Embeddings;

/// <summary>
/// CLIP 模型 Embedding 服务接口
/// </summary>
public interface IClipEmbeddingService : IDisposable
{
    /// <summary>
    /// 生成单张图片的 512 维向量
    /// </summary>
    Task<float[]> GetImageEmbeddingAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// 批量生成图片的 512 维向量
    /// </summary>
    Task<float[][]> GetImageEmbeddingsBatchAsync(IReadOnlyList<string> imagePaths, CancellationToken ct = default);

    /// <summary>
    /// 生成文本的 512 维向量
    /// </summary>
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);
}
