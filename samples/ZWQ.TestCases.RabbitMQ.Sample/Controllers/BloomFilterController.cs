using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.Redis.BloomFilter;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 布隆过滤器测试接口 — 缓存穿透防护第一线
/// </summary>
[ApiController]
[Route("api/bloom")]
public class BloomFilterController : ControllerBase
{
    private readonly IBloomFilter _bloomFilter;

    public BloomFilterController(IBloomFilter bloomFilter)
    {
        _bloomFilter = bloomFilter;
    }

    /// <summary>
    /// 初始化布隆过滤器（必须先调用）
    /// </summary>
    [HttpPost("init/{key}")]
    public async Task<IActionResult> Initialize(
        string key,
        [FromQuery] int expectedInsertions = 100000,
        [FromQuery] double falsePositiveRate = 0.01)
    {
        await _bloomFilter.InitializeAsync(key, expectedInsertions, falsePositiveRate);
        var info = await _bloomFilter.GetInfoAsync(key);

        return Ok(new
        {
            message = $"布隆过滤器 '{key}' 初始化完成",
            expectedInsertions,
            falsePositiveRate,
            info
        });
    }

    /// <summary>
    /// 添加单个元素
    /// </summary>
    [HttpPost("add/{key}")]
    public async Task<IActionResult> Add(string key, [FromBody] BloomAddRequest request)
    {
        await _bloomFilter.AddAsync(key, request.Item);
        return Ok(new { message = $"已添加: {request.Item}", key });
    }

    /// <summary>
    /// 批量添加元素
    /// </summary>
    [HttpPost("add-many/{key}")]
    public async Task<IActionResult> AddMany(string key, [FromBody] BloomAddManyRequest request)
    {
        await _bloomFilter.AddManyAsync(key, request.Items);
        return Ok(new { message = $"已批量添加 {request.Items.Count} 个元素", key });
    }

    /// <summary>
    /// 检查元素是否可能存在
    /// </summary>
    [HttpGet("check/{key}/{item}")]
    public async Task<IActionResult> Check(string key, string item)
    {
        var exists = await _bloomFilter.ContainsAsync(key, item);
        return Ok(new
        {
            key,
            item,
            mayExist = exists,
            description = exists ? "可能存在（需进一步确认）" : "一定不存在（无需查库）"
        });
    }

    /// <summary>
    /// 批量检查
    /// </summary>
    [HttpPost("check-many/{key}")]
    public async Task<IActionResult> CheckMany(string key, [FromBody] BloomAddManyRequest request)
    {
        var results = await _bloomFilter.ContainsManyAsync(key, request.Items);
        var response = request.Items.Select((item, i) => new
        {
            item,
            mayExist = results[i]
        });

        return Ok(new { key, results = response });
    }

    /// <summary>
    /// 查看布隆过滤器统计信息
    /// </summary>
    [HttpGet("info/{key}")]
    public async Task<IActionResult> GetInfo(string key)
    {
        var info = await _bloomFilter.GetInfoAsync(key);
        return Ok(info);
    }

    /// <summary>
    /// 删除布隆过滤器
    /// </summary>
    [HttpDelete("delete/{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        var deleted = await _bloomFilter.DeleteAsync(key);
        return Ok(new { key, deleted });
    }
}

// ====== DTO ======

public class BloomAddRequest
{
    public string Item { get; set; } = "";
}

public class BloomAddManyRequest
{
    public List<string> Items { get; set; } = new();
}
