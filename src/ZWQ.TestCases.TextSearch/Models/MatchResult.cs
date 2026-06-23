namespace ZWQ.TestCases.TextSearch.Models;

/// <summary>
/// 单个关键词的搜索结果
/// </summary>
public sealed class MatchResult
{
    /// <summary>搜索的关键词</summary>
    public required string Keyword { get; init; }

    /// <summary>出现的所有位置</summary>
    public required IReadOnlyList<Position> Positions { get; init; }

    /// <summary>总匹配次数</summary>
    public int TotalCount => Positions.Count;

    /// <summary>是否通过全文扫描（降级策略）找到</summary>
    public bool FoundViaFullTextScan { get; init; }

    /// <summary>是否为本次搜索新学习的词</summary>
    public bool NewlyLearned { get; init; }
}
