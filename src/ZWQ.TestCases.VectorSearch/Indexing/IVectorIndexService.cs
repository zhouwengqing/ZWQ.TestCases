namespace ZWQ.TestCases.VectorSearch.Indexing;

/// <summary>
/// 向量索引服务接口
/// </summary>
public interface IVectorIndexService
{
    /// <summary>索引单张图片</summary>
    Task IndexImageAsync(string imagePath, CancellationToken ct = default);

    /// <summary>批量索引图片</summary>
    Task IndexBatchAsync(IReadOnlyList<string> imagePaths, CancellationToken ct = default);

    /// <summary>扫描目录并索引所有支持的图片文件</summary>
    Task<int> IndexDirectoryAsync(string directory, CancellationToken ct = default);
}
