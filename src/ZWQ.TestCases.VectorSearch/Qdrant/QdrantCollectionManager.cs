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
            _logger.LogInformation("[Qdrant] 正在创建集合 '{Name}' (维度={Dim}, 余弦距离)",
                _options.CollectionName, _clipOptions.EmbeddingDimension);

            await _client.CreateCollectionAsync(
                _options.CollectionName,
                new VectorParams { Size = (ulong)_clipOptions.EmbeddingDimension, Distance = Distance.Cosine },
                cancellationToken: ct);

            await _client.CreatePayloadIndexAsync(_options.CollectionName, "file_path", PayloadSchemaType.Keyword, cancellationToken: ct);
            await _client.CreatePayloadIndexAsync(_options.CollectionName, "sha256_hash", PayloadSchemaType.Keyword, cancellationToken: ct);

            _logger.LogInformation("[Qdrant] 集合 '{Name}' 创建完成, 已建立载荷索引", _options.CollectionName);
        }
        else
        {
            _logger.LogInformation("[Qdrant] 集合 '{Name}' 已存在", _options.CollectionName);
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
        _logger.LogInformation("[Qdrant] 已写入 {Count} 个向量点到 '{Name}'", pointStructs.Count, _options.CollectionName);
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

    public async Task<HashSet<string>> GetExistingFilePathsAsync(CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_options.CollectionName, ct);
        if (!exists) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseUrl = _options.UseHttps ? "https" : "http";
        var scrollUrl = $"{baseUrl}://{_options.Host}:{_options.HttpPort}/collections/{_options.CollectionName}/points/scroll";

        using var http = new HttpClient();
        object? offset = null;

        do
        {
            var body = new
            {
                limit = 1000,
                offset,
                with_payload = new { include = new[] { "file_path" } }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync(scrollUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var result = doc.RootElement.GetProperty("result");
            var points = result.GetProperty("points");

            foreach (var point in points.EnumerateArray())
            {
                if (point.TryGetProperty("payload", out var payload) &&
                    payload.TryGetProperty("file_path", out var fp) &&
                    fp.GetString() is { } path && !string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            // Get next page offset (can be string UUID or number)
            var nextOffset = result.GetProperty("next_page_offset");
            offset = nextOffset.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Number => (object?)nextOffset.GetUInt64(),
                System.Text.Json.JsonValueKind.String => (object?)nextOffset.GetString(),
                _ => null
            };

        } while (offset is not null);

        _logger.LogInformation("[Qdrant] 在 '{Name}' 中发现 {Count} 个已索引路径", _options.CollectionName, paths.Count);
        return paths;
    }
}
