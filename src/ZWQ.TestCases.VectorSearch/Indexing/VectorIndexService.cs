using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
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

        _logger.LogInformation("[索引] 正在索引图片 {Path} -> {PointId}", fullPath, pointId);

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
            if (!File.Exists(full))
            {
                _logger.LogWarning("[索引] 图片不存在, 已跳过: {Path}", full);
                continue;
            }

            // 校验文件是否为合法图片格式（只读文件头，不完全加载）
            try
            {
                var info = Image.Identify(full);
                if (info == null)
                {
                    _logger.LogWarning("[索引] 无法识别图片格式, 已跳过: {Path}", full);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[索引] 图片校验失败（可能损坏或非图片文件）, 已跳过: {Path} — {Reason}", full, ex.Message);
                continue;
            }

            validPaths.Add(full);
        }

        if (validPaths.Count == 0) return;

        _logger.LogInformation("[索引] 批量索引 {Count} 张图片", validPaths.Count);

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
        _logger.LogInformation("[索引] 批量完成: {Count} 张图片已索引", validPaths.Count);
    }

    public async Task<int> IndexDirectoryAsync(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("[索引] 目录不存在: {Dir}", directory);
            return 0;
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => _options.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _logger.LogInformation("[索引] 在 {Dir} 中发现 {Count} 张图片", directory, files.Count);

        // 查询已索引路径，跳过重复
        var existingPaths = await _qdrant.GetExistingFilePathsAsync(ct);
        var newFiles = files
            .Where(f => !existingPaths.Contains(Path.GetFullPath(f)))
            .ToList();

        int skipped = files.Count - newFiles.Count;
        if (skipped > 0)
            _logger.LogInformation("[索引] 跳过 {Skipped} 张已索引图片, 剩余 {Remaining} 张待索引", skipped, newFiles.Count);

        if (newFiles.Count == 0)
        {
            _logger.LogInformation("[索引] 所有图片已索引, 无需处理");
            return 0;
        }

        int totalIndexed = 0;
        foreach (var batch in newFiles.Chunk(_options.BatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await IndexBatchAsync(batch, ct);
                totalIndexed += batch.Length;
                _logger.LogInformation("[索引] 进度: {Done}/{Total}", totalIndexed, newFiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[索引] 批次失败, 跳过 {Count} 张图片", batch.Length);
            }
        }

        _logger.LogInformation("[索引] 目录索引完成, 共计 {Count} 张", totalIndexed);
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
