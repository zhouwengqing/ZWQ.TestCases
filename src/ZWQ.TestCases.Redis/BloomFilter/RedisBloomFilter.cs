using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZWQ.TestCases.Redis.Connection;

namespace ZWQ.TestCases.Redis.BloomFilter;

/// <summary>
/// 基于 Redis BitArray 的布隆过滤器实现
/// 
/// 原理：
///   1. 使用 K 个哈希函数将元素映射到位数组的 K 个位置
///   2. 添加元素：将 K 个位置都置为 1
///   3. 查询元素：检查 K 个位置是否全为 1
///      - 全为 1 → 可能存在
///      - 有 0   → 一定不存在
/// 
/// 哈希策略：使用 MurmurHash 风格的双重哈希模拟 K 个独立哈希函数
///   h_i(x) = (h1(x) + i * h2(x)) % m
/// </summary>
public class RedisBloomFilter : IBloomFilter
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly ILogger<RedisBloomFilter> _logger;

    /// <summary>布隆过滤器元数据存储在 Redis Hash 中的字段名</summary>
    private const string MetaField_Size = "size";
    private const string MetaField_HashCount = "hash_count";

    public RedisBloomFilter(RedisConnectionManager connectionManager, ILogger<RedisBloomFilter> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    private IDatabase Db => _connectionManager.GetDatabase();

    // ====== 布隆过滤器核心操作 ======

    public async Task InitializeAsync(string key, int expectedInsertions, double falsePositiveRate = 0.01)
    {
        // 计算最优位数组大小和哈希函数数量
        var bitSize = CalculateBitSize(expectedInsertions, falsePositiveRate);
        var hashCount = CalculateHashCount(bitSize, expectedInsertions);

        var bitKey = GetBitKey(key);
        var metaKey = GetMetaKey(key);

        // 初始化位数组（全部置 0）
        await Db.StringSetBitAsync(bitKey, 0, false); // 确保 key 存在
        // 设置位数组大小（用 SETBIT 设置最后一位来分配空间）
        await Db.StringSetBitAsync(bitKey, bitSize - 1, false);

        // 保存元数据
        await Db.HashSetAsync(metaKey, new HashEntry[]
        {
            new(MetaField_Size, bitSize),
            new(MetaField_HashCount, hashCount)
        });

        _logger.LogInformation(
            "[BloomFilter] 初始化完成: {Key}, 预期容量={Expected}, 位数组大小={BitSize}, 哈希函数数={HashCount}, 误判率={FPR:P}",
            key, expectedInsertions, bitSize, hashCount, falsePositiveRate);
    }

    public async Task AddAsync(string key, string item)
    {
        var (bitSize, hashCount) = await GetConfigAsync(key);
        var positions = GetHashPositions(item, bitSize, hashCount);

        var bitKey = GetBitKey(key);
        foreach (var pos in positions)
        {
            await Db.StringSetBitAsync(bitKey, pos, true);
        }
    }

    public async Task AddManyAsync(string key, IEnumerable<string> items)
    {
        var (bitSize, hashCount) = await GetConfigAsync(key);
        var bitKey = GetBitKey(key);
        var batch = Db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var item in items)
        {
            var positions = GetHashPositions(item, bitSize, hashCount);
            foreach (var pos in positions)
            {
                tasks.Add(batch.StringSetBitAsync(bitKey, pos, true));
            }
        }

        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async Task<bool> ContainsAsync(string key, string item)
    {
        var (bitSize, hashCount) = await GetConfigAsync(key);
        var positions = GetHashPositions(item, bitSize, hashCount);

        var bitKey = GetBitKey(key);
        foreach (var pos in positions)
        {
            var bit = await Db.StringGetBitAsync(bitKey, pos);
            if (!bit) return false; // 有一位为 0 → 一定不存在
        }
        return true; // 所有位都为 1 → 可能存在
    }

    public async Task<bool[]> ContainsManyAsync(string key, IEnumerable<string> items)
    {
        var (bitSize, hashCount) = await GetConfigAsync(key);
        var bitKey = GetBitKey(key);
        var itemList = items.ToList();
        var results = new bool[itemList.Count];

        for (int i = 0; i < itemList.Count; i++)
        {
            var positions = GetHashPositions(itemList[i], bitSize, hashCount);
            var allSet = true;
            foreach (var pos in positions)
            {
                var bit = await Db.StringGetBitAsync(bitKey, pos);
                if (!bit) { allSet = false; break; }
            }
            results[i] = allSet;
        }

        return results;
    }

    public async Task<bool> DeleteAsync(string key)
    {
        var bitKey = GetBitKey(key);
        var metaKey = GetMetaKey(key);
        var r1 = await Db.KeyDeleteAsync(bitKey);
        var r2 = await Db.KeyDeleteAsync(metaKey);
        return r1 || r2;
    }

    public async Task<BloomFilterInfo> GetInfoAsync(string key)
    {
        var (bitSize, hashCount) = await GetConfigAsync(key);
        var bitKey = GetBitKey(key);

        var bitCount = await Db.StringBitCountAsync(bitKey);
        var fillRatio = bitSize > 0 ? (double)bitCount / bitSize : 0;

        return new BloomFilterInfo
        {
            Key = key,
            BitArraySize = bitSize,
            HashFunctionCount = hashCount,
            SetBits = bitCount,
            FillRatio = fillRatio
        };
    }

    // ====== 哈希策略 ======

    /// <summary>
    /// 使用双重哈希模拟 K 个独立哈希函数
    /// h_i(x) = (h1(x) + i * h2(x)) % m
    /// </summary>
    private static long[] GetHashPositions(string item, long bitSize, int hashCount)
    {
        var (h1, h2) = ComputeDualHash(item);
        var positions = new long[hashCount];

        for (int i = 0; i < hashCount; i++)
        {
            var combined = h1 + (long)i * h2;
            positions[i] = ((combined % bitSize) + bitSize) % bitSize; // 确保非负
        }

        return positions;
    }

    /// <summary>
    /// 双重哈希：用 MD5 的前 16 字节生成两个独立的 64 位哈希值
    /// </summary>
    private static (long h1, long h2) ComputeDualHash(string item)
    {
        var bytes = Encoding.UTF8.GetBytes(item);
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(bytes);

        long h1 = BitConverter.ToInt64(hash, 0);
        long h2 = BitConverter.ToInt64(hash, 8);

        return (h1, h2);
    }

    // ====== 数学计算 ======

    /// <summary>
    /// 计算最优位数组大小: m = -n * ln(p) / (ln(2))^2
    /// </summary>
    private static long CalculateBitSize(int expectedInsertions, double falsePositiveRate)
    {
        var n = (double)expectedInsertions;
        var p = falsePositiveRate;
        var m = -n * Math.Log(p) / (Math.Log(2) * Math.Log(2));
        return Math.Max((long)Math.Ceiling(m), 64); // 至少 64 位
    }

    /// <summary>
    /// 计算最优哈希函数数量: k = (m/n) * ln(2)
    /// </summary>
    private static int CalculateHashCount(long bitSize, int expectedInsertions)
    {
        var k = (double)bitSize / expectedInsertions * Math.Log(2);
        return Math.Max((int)Math.Ceiling(k), 1); // 至少 1 个
    }

    // ====== 辅助方法 ======

    private async Task<(long bitSize, int hashCount)> GetConfigAsync(string key)
    {
        var metaKey = GetMetaKey(key);
        var sizeVal = await Db.HashGetAsync(metaKey, MetaField_Size);
        var hashVal = await Db.HashGetAsync(metaKey, MetaField_HashCount);

        if (sizeVal.IsNullOrEmpty || hashVal.IsNullOrEmpty)
            throw new InvalidOperationException(
                $"[BloomFilter] 布隆过滤器 '{key}' 未初始化，请先调用 InitializeAsync");

        return ((long)sizeVal, (int)hashVal);
    }

    private static string GetBitKey(string key) => $"bloom:{key}:bits";
    private static string GetMetaKey(string key) => $"bloom:{key}:meta";
}
