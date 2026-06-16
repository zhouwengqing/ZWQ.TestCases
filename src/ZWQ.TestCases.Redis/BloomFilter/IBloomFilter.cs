using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZWQ.TestCases.Redis.BloomFilter;

/// <summary>
/// 布隆过滤器接口
/// 基于 Redis BitArray 实现，用于缓存穿透防护的第一线
/// 特点：判断"不存在"100% 准确，判断"可能存在"有极低误判率
/// </summary>
public interface IBloomFilter
{
    /// <summary>
    /// 向布隆过滤器中添加一个元素
    /// </summary>
    Task AddAsync(string key, string item);

    /// <summary>
    /// 批量添加元素
    /// </summary>
    Task AddManyAsync(string key, IEnumerable<string> items);

    /// <summary>
    /// 检查元素是否可能存在
    /// </summary>
    /// <returns>false = 一定不存在，true = 可能存在（有误判率）</returns>
    Task<bool> ContainsAsync(string key, string item);

    /// <summary>
    /// 批量检查
    /// </summary>
    Task<bool[]> ContainsManyAsync(string key, IEnumerable<string> items);

    /// <summary>
    /// 创建（或重建）布隆过滤器
    /// </summary>
    /// <param name="key">过滤器名称</param>
    /// <param name="expectedInsertions">预期插入量（决定位数组大小）</param>
    /// <param name="falsePositiveRate">期望误判率（0.01 = 1%）</param>
    Task InitializeAsync(string key, int expectedInsertions, double falsePositiveRate = 0.01);

    /// <summary>
    /// 删除布隆过滤器
    /// </summary>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// 获取布隆过滤器统计信息
    /// </summary>
    Task<BloomFilterInfo> GetInfoAsync(string key);
}

/// <summary>
/// 布隆过滤器统计信息
/// </summary>
public class BloomFilterInfo
{
    public string Key { get; set; } = "";
    public long BitArraySize { get; set; }
    public int HashFunctionCount { get; set; }
    public long SetBits { get; set; }
    public double FillRatio { get; set; }
}
