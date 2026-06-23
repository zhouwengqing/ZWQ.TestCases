using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using ZWQ.TestCases.VectorSearch.Models;
using ZWQ.TestCases.VectorSearch.Options;

namespace ZWQ.TestCases.VectorSearch.Qdrant;

/// <summary>
/// Qdrant 集合管理器 - 管理集合创建、数据写入和向量搜索
/// </summary>
public sealed class QdrantCollectionManager : IQdrantCollectionManager
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ClipModelOptions _clipOptions;
    private readonly ILogger<QdrantCollectionManager> _logger;
    private bool _collectionEnsured;

    public QdrantCollectionManager(
        QdrantClient client,
        IOptions<QdrantOptions> options,
        IOptions<ClipModelOptions> clipOptions,
        ILogger<QdrantCollectionManager> logger)
    {
        _client = client;
        _options = options.Value;
        _clipOptions = clipOptions.Value;
        _logger = logger;
    }

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        if (_collectionEnsured) return;

        var exists = await _client.CollectionExistsAsync(_options.CollectionName, ct);
        if (!exists)
        {
            _logger.LogInformation("[Qdrant] Creating collection '{Name}' (dim={Dim}, Cosine)",
                _options.CollectionName, _clipOptions.EmbeddingDimension);

            await _client.CreateCollectionAsync(
                _options.CollectionName,
                new VectorParams { Size = (ulong)_clipOptions.EmbeddingDimension, Distance = Distance.Cosine },
                cancellationToken: ct);

            await _client.CreatePayloadIndexAsync(_options.CollectionName, "file_path", PayloadSchemaType.Keyword, cancellationToken: ct);
            await _client.CreatePayloadIndexAsync(_options.CollectionName, "sha256_hash", PayloadSchemaType.Keyword, cancellationToken: ct);

            _logger.LogInformation("[Qdrant] Collection '{Name}' created with payload indices", _options.CollectionName);
        }
        else
        {
            _logger.LogInformation("[Qdrant] Collection '{Name}' already exists", _options.CollectionName);
        }

        _collectionEnsured = true;
    }

    public async Task UpsertBatchAsync(
        IReadOnlyList<(Guid PointId, float[] Vector, ImageDocument Payload)> points,
        CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        var pointStructs = new List<PointStruct>(points.Count);
        foreach (var (pointId, vector, payload) in points)
        {
            pointStructs.Add(new PointStruct
            {
                Id = pointId,
                Vectors = vector,
                Payload =
                {
                    ["file_path"] = payload.FilePath,
                    ["file_name"] = payload.FileName,
                    ["file_size_bytes"] = payload.FileSizeBytes,
                    ["indexed_at_utc"] = payload.IndexedAtUtc.ToString("O"),
                    ["sha256_hash"] = payload.Sha256Hash
                }
            });
        }

        await _client.UpsertAsync(_options.CollectionName, pointStructs, cancellationToken: ct);
        _logger.LogInformation("[Qdrant] Upserted {Count} points into '{Name}'", pointStructs.Count, _options.CollectionName);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector, int topK = 10, float? scoreThreshold = null, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        var results = await _client.SearchAsync(
            _options.CollectionName,
            queryVector,
            limit: (ulong)topK,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        var searchResults = new List<SearchResult>(results.Count);
        foreach (var point in results)
        {
            var doc = new ImageDocument
            {
                FilePath = point.Payload.GetValueOrDefault("file_path")?.StringValue ?? string.Empty,
                FileName = point.Payload.GetValueOrDefault("file_name")?.StringValue ?? string.Empty,
                FileSizeBytes = (long)(point.Payload.GetValueOrDefault("file_size_bytes")?.IntegerValue ?? 0),
                IndexedAtUtc = DateTime.TryParse(point.Payload.GetValueOrDefault("indexed_at_utc")?.StringValue, out var dt) ? dt : DateTime.MinValue,
                Sha256Hash = point.Payload.GetValueOrDefault("sha256_hash")?.StringValue ?? string.Empty
            };

            Guid.TryParse(point.Id.Uuid, out var id);
            searchResults.Add(new SearchResult { PointId = id, Score = point.Score, Document = doc });
        }

        return searchResults;
    }

    public Guid ComputePointId(string imagePath)
    {
        var normalized = Path.GetFullPath(imagePath).ToLowerInvariant();
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash.AsSpan(0, 16));
    }
}
