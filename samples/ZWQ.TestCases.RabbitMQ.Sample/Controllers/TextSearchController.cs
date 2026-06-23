using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.TextSearch;
using ZWQ.TestCases.TextSearch.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 文本搜索接口 - 倒排索引 + Jieba 中文分词 + 自适应学习
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class TextSearchController : ControllerBase
{
    private readonly ITextSearchService _searchService;

    public TextSearchController(ITextSearchService searchService) => _searchService = searchService;

    /// <summary>
    /// 构建倒排索引 - 对指定文件进行 Jieba 分词并建立倒排索引
    /// </summary>
    /// <param name="request">包含文件路径的请求体</param>
    [HttpPost("build")]
    public async Task<IActionResult> BuildIndex([FromBody] BuildIndexRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest("文件路径不能为空");

        if (!System.IO.File.Exists(request.FilePath))
            return NotFound($"文件不存在: {request.FilePath}");

        await _searchService.BuildIndexAsync(request.FilePath);
        var stats = _searchService.GetStats();

        return Ok(new
        {
            message = "索引构建完成",
            stats.FilePath,
            fileSizeMB = Math.Round(stats.FileSizeBytes / (1024.0 * 1024), 2),
            stats.LineCount,
            stats.WordCount,
            stats.TotalEntries,
            stats.LearnedWordCount,
            stats.IsBuilt
        });
    }

    /// <summary>
    /// 搜索关键词 - 在已索引的文本中搜索一个或多个关键词
    /// </summary>
    /// <param name="request">搜索请求：关键词列表 + 是否区分大小写</param>
    [HttpPost("search")]
    public async Task<ActionResult<TextSearchSummary>> Search([FromBody] SearchRequest request)
    {
        if (!_searchService.IsIndexBuilt)
            return BadRequest("请先调用 POST /api/textsearch/build 构建索引");

        if (request.Keywords is null || request.Keywords.Count == 0)
            return BadRequest("至少提供一个关键词");

        var summary = await _searchService.SearchAsync(request.Keywords, request.CaseSensitive);
        return Ok(summary);
    }

    /// <summary>
    /// 索引统计 - 获取当前索引的状态信息
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        if (!_searchService.IsIndexBuilt)
            return Ok(new { message = "尚未构建索引", isBuilt = false });

        var stats = _searchService.GetStats();
        return Ok(new
        {
            stats.FilePath,
            fileSizeMB = Math.Round(stats.FileSizeBytes / (1024.0 * 1024), 2),
            stats.LineCount,
            stats.WordCount,
            stats.TotalEntries,
            stats.LearnedWordCount,
            stats.IsBuilt
        });
    }
}

/// <summary>
/// 构建索引请求体
/// </summary>
public class BuildIndexRequest
{
    /// <summary>
    /// 要索引的文件完整路径，例如 D:\TestFiles\test_15mb.txt
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// 搜索请求体
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// 要搜索的关键词列表
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// 是否区分大小写（默认 false）
    /// </summary>
    public bool CaseSensitive { get; set; } = false;
}
