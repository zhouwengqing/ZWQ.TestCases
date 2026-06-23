using System.Collections.Concurrent;
using ZWQ.TestCases.TextSearch.Models;

namespace ZWQ.TestCases.TextSearch.Index;

/// <summary>
/// 倒排索引（Inverted Index）。
/// 以词为键，记录该词在文本中出现的所有位置（行号 + 列索引）。
/// 线程安全，支持并发读写。
/// </summary>
public sealed class InvertedIndex
{
    // 索引核心：词 -> 位置列表。Key 统一使用小写，实现不区分大小写的查找。
    private readonly ConcurrentDictionary<string, List<Position>> _index = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 将一个词的位置信息加入索引（线程安全）
    /// </summary>
    public void Add(string word, Position position)
    {
        var positions = _index.GetOrAdd(word, _ => new List<Position>());
        lock (positions)
        {
            positions.Add(position);
        }
    }

    /// <summary>
    /// 批量添加同一词的多个位置
    /// </summary>
    public void AddRange(string word, IEnumerable<Position> positionsToAdd)
    {
        var positions = _index.GetOrAdd(word, _ => new List<Position>());
        lock (positions)
        {
            positions.AddRange(positionsToAdd);
        }
    }

    /// <summary>
    /// 查找词语在文本中的所有位置。返回 null 表示索引中不存在该词。
    /// </summary>
    public IReadOnlyList<Position>? Lookup(string word)
    {
        if (!_index.TryGetValue(word, out var positions))
            return null;

        lock (positions)
        {
            return positions.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 索引中是否包含该词
    /// </summary>
    public bool Contains(string word)
    {
        return _index.ContainsKey(word);
    }

    /// <summary>
    /// 索引中的不同词数量
    /// </summary>
    public int WordCount => _index.Count;

    /// <summary>
    /// 索引中的总位置条目数（所有词的出现次数之和）
    /// </summary>
    public long TotalEntries
    {
        get
        {
            long total = 0;
            foreach (var kvp in _index)
            {
                lock (kvp.Value)
                {
                    total += kvp.Value.Count;
                }
            }
            return total;
        }
    }

    /// <summary>
    /// 清空所有索引数据
    /// </summary>
    public void Clear()
    {
        _index.Clear();
    }
}
