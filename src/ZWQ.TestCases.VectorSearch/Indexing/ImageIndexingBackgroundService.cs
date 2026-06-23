using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZWQ.TestCases.VectorSearch.Options;

namespace ZWQ.TestCases.VectorSearch.Indexing;

/// <summary>
/// 启动时自动批量索引配置目录下的所有图片，完成后进入空闲状态
/// </summary>
public sealed class ImageIndexingBackgroundService : BackgroundService
{
    private readonly IVectorIndexService _indexService;
    private readonly VectorSearchOptions _options;
    private readonly ILogger<ImageIndexingBackgroundService> _logger;

    public ImageIndexingBackgroundService(
        IVectorIndexService indexService,
        IOptions<VectorSearchOptions> options,
        ILogger<ImageIndexingBackgroundService> logger)
    {
        _indexService = indexService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Indexer] Background service starting for {Dir}", _options.ImageDirectory);

        if (!Directory.Exists(_options.ImageDirectory))
        {
            _logger.LogWarning("[Indexer] Image directory does not exist: {Dir}. Skipping initial indexing.", _options.ImageDirectory);
            return;
        }

        try
        {
            int count = await _indexService.IndexDirectoryAsync(_options.ImageDirectory, stoppingToken);
            _logger.LogInformation("[Indexer] Initial indexing complete. {Count} images indexed.", count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[Indexer] Indexing cancelled due to application shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Indexer] Unhandled error during initial image indexing.");
        }

        _logger.LogInformation("[Indexer] Background service idle. Use API for incremental indexing.");
    }
}
