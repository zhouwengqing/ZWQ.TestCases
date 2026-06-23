namespace ZWQ.TestCases.TextSearch.Models;

/// <summary>
/// 文本搜索的整体结果摘要
/// </summary>
public sealed class TextSearchSummary
{
    /// <summary>搜索的关键词列表</summary>
    public required IReadOnlyList<string> Keywords { get; init; }

    /// <summary>每个关键词的匹配结果</summary>
    public required IReadOnlyList<MatchResult> MatchResults { get; init; }

    /// <summary>总匹配次数（所有关键词合计）</summary>
    public int TotalMatches => MatchResults.Sum(r => r.TotalCount);

    /// <summary>搜索耗时（毫秒）</summary>
    public long SearchTimeMs { get; init; }

    /// <summary>是否有通过全文扫描（降级）命中的结果</summary>
    public bool UsedFullTextScan => MatchResults.Any(r => r.FoundViaFullTextScan);

    /// <summary>本次搜索是否学习到了新词</summary>
    public bool LearnedNewWord => MatchResults.Any(r => r.NewlyLearned);

    /// <summary>已索引文件的完整路径</summary>
    public string? IndexedFilePath { get; init; }
}
