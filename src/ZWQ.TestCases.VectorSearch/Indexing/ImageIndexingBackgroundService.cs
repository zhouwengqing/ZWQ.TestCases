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
        _logger.LogInformation("[索引] 后台索引服务启动, 目标目录: {Dir}", _options.ImageDirectory);

        if (!Directory.Exists(_options.ImageDirectory))
        {
            _logger.LogWarning("[索引] 图片目录不存在: {Dir}, 跳过初始索引", _options.ImageDirectory);
            return;
        }

        try
        {
            int count = await _indexService.IndexDirectoryAsync(_options.ImageDirectory, stoppingToken);
            _logger.LogInformation("[索引] 初始索引完成, 共索引 {Count} 张图片", count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[索引] 应用关闭, 索引任务已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[索引] 初始索引过程中发生未处理异常");
        }

        _logger.LogInformation("[索引] 后台服务进入空闲状态, 可通过 API 增量索引");
    }
}
