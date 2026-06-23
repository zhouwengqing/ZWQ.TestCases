using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.VectorSearch.Indexing;
using ZWQ.TestCases.VectorSearch.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 图片索引管理接口 - 支持增量索引和全量扫描索引
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class IndexingController : ControllerBase
{
    private readonly IVectorIndexService _indexService;

    public IndexingController(IVectorIndexService indexService) => _indexService = indexService;

    /// <summary>
    /// 批量索引指定路径的图片
    /// </summary>
    [HttpPost("index")]
    public async Task<IActionResult> IndexImages([FromBody] IndexingRequest request)
    {
        if (request.ImagePaths is null || request.ImagePaths.Count == 0)
            return BadRequest("At least one image path is required.");

        await _indexService.IndexBatchAsync(request.ImagePaths);
        return Ok(new { indexed = request.ImagePaths.Count });
    }

    /// <summary>
    /// 索引单张图片
    /// </summary>
    /// <param name="imagePath">图片文件路径</param>
    [HttpPost("index/single")]
    public async Task<IActionResult> IndexSingleImage([FromQuery] string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return BadRequest("Image path is required.");

        await _indexService.IndexImageAsync(imagePath);
        return Ok(new { indexed = 1, path = Path.GetFullPath(imagePath) });
    }

    /// <summary>
    /// 扫描目录并全量索引所有支持的图片文件
    /// </summary>
    /// <param name="directory">图片目录路径</param>
    [HttpPost("index/directory")]
    public async Task<IActionResult> IndexDirectory([FromQuery] string? directory = null)
    {
        var dir = directory ?? @"D:\Images";
        int count = await _indexService.IndexDirectoryAsync(dir);
        return Ok(new { indexed = count, directory = dir });
    }
}
