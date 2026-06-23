using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.VectorSearch.Models;
using ZWQ.TestCases.VectorSearch.Search;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 向量搜索接口 - 支持文字搜图和以图搜图
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SearchController : ControllerBase
{
    private readonly IVectorSearchService _searchService;

    public SearchController(IVectorSearchService searchService) => _searchService = searchService;

    /// <summary>
    /// 文字搜图 - 使用自然语言搜索相似图片
    /// </summary>
    /// <param name="query">自然语言搜索文本</param>
    /// <param name="topK">返回结果数量（默认 10）</param>
    /// <param name="scoreThreshold">最低余弦相似度阈值（0~1）</param>
    [HttpGet("text")]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> SearchByText(
        [FromQuery] string query,
        [FromQuery] int topK = 10,
        [FromQuery] float? scoreThreshold = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query cannot be empty.");

        var results = await _searchService.TextToImageSearchAsync(query, topK, scoreThreshold);
        return Ok(results);
    }

    /// <summary>
    /// 以图搜图 - 上传图片搜索视觉相似的图片
    /// </summary>
    /// <param name="image">上传的图片文件</param>
    /// <param name="topK">返回结果数量（默认 10）</param>
    /// <param name="scoreThreshold">最低余弦相似度阈值（0~1）</param>
    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> SearchByImage(
        IFormFile image,
        [FromQuery] int topK = 10,
        [FromQuery] float? scoreThreshold = null)
    {
        if (image is null || image.Length == 0)
            return BadRequest("Image file is required.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"clip_search_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}");
        try
        {
            using (var stream = System.IO.File.Create(tempPath))
                await image.CopyToAsync(stream);

            var results = await _searchService.ImageToImageSearchAsync(tempPath, topK, scoreThreshold);
            return Ok(results);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    /// <summary>
    /// 以图搜图 - 使用已索引图片的路径搜索相似图片
    /// </summary>
    /// <param name="imagePath">已索引图片的文件路径</param>
    /// <param name="topK">返回结果数量（默认 10）</param>
    /// <param name="scoreThreshold">最低余弦相似度阈值（0~1）</param>
    [HttpGet("image")]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> SearchByImagePath(
        [FromQuery] string imagePath,
        [FromQuery] int topK = 10,
        [FromQuery] float? scoreThreshold = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return BadRequest("Image path is required.");

        var results = await _searchService.ImageToImageSearchAsync(imagePath, topK, scoreThreshold);
        return Ok(results);
    }
}
