using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Models;
using ZWQ.TestCases.VectorSearch.Options;
using ZWQ.TestCases.VectorSearch.Qdrant;

namespace ZWQ.TestCases.VectorSearch.Indexing;

/// <summary>
/// 向量索引服务 - 调用 CLIP 生成向量并写入 Qdrant
/// </summary>
public sealed class VectorIndexService : IVectorIndexService
{
    private readonly IClipEmbeddingService _embeddingService;
    private readonly IQdrantCollectionManager _qdrant;
    private readonly VectorSearchOptions _options;
    private readonly ILogger<VectorIndexService> _logger;

    public VectorIndexService(
        IClipEmbeddingService embeddingService,
        IQdrantCollectionManager qdrant,
        IOptions<VectorSearchOptions> options,
        ILogger<VectorIndexService> logger)
    {
        _embeddingService = embeddingService;
        _qdrant = qdrant;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IndexImageAsync(string imagePath, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(imagePath);
        var pointId = _qdrant.ComputePointId(fullPath);

        _logger.LogInformation("[Indexer] Indexing image {Path} -> {PointId}", fullPath, pointId);

        float[] embedding = await _embeddingService.GetImageEmbeddingAsync(fullPath, ct);
        var fileInfo = new FileInfo(fullPath);

        var doc = new ImageDocument
        {
            FilePath = fullPath,
            FileName = fileInfo.Name,
            FileSizeBytes = fileInfo.Length,
            IndexedAtUtc = DateTime.UtcNow,
            Sha256Hash = ComputeFileSha256(fullPath)
        };

        await _qdrant.UpsertBatchAsync([(pointId, embedding, doc)], ct);
    }

    public async Task IndexBatchAsync(IReadOnlyList<string> imagePaths, CancellationToken ct = default)
    {
        var validPaths = new List<string>();
        foreach (var p in imagePaths)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full))
                validPaths.Add(full);
            else
                _logger.LogWarning("[Indexer] Image not found, skipping: {Path}", full);
        }

        if (validPaths.Count == 0) return;

        _logger.LogInformation("[Indexer] Batch indexing {Count} images", validPaths.Count);

        float[][] embeddings = await _embeddingService.GetImageEmbeddingsBatchAsync(validPaths, ct);

        var points = new List<(Guid, float[], ImageDocument)>(validPaths.Count);
        for (int i = 0; i < validPaths.Count; i++)
        {
            var fileInfo = new FileInfo(validPaths[i]);
            var doc = new ImageDocument
            {
                FilePath = validPaths[i],
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                IndexedAtUtc = DateTime.UtcNow,
                Sha256Hash = ComputeFileSha256(validPaths[i])
            };
            points.Add((_qdrant.ComputePointId(validPaths[i]), embeddings[i], doc));
        }

        await _qdrant.UpsertBatchAsync(points, ct);
        _logger.LogInformation("[Indexer] Batch complete: {Count} images indexed", validPaths.Count);
    }

    public async Task<int> IndexDirectoryAsync(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("[Indexer] Directory not found: {Dir}", directory);
            return 0;
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => _options.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _logger.LogInformation("[Indexer] Found {Count} images in {Dir}", files.Count, directory);

        int totalIndexed = 0;
        foreach (var batch in files.Chunk(_options.BatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await IndexBatchAsync(batch, ct);
                totalIndexed += batch.Length;
                _logger.LogInformation("[Indexer] Progress: {Done}/{Total}", totalIndexed, files.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Indexer] Batch failed, skipping {Count} images", batch.Length);
            }
        }

        _logger.LogInformation("[Indexer] Directory indexing complete. Total: {Count}", totalIndexed);
        return totalIndexed;
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
