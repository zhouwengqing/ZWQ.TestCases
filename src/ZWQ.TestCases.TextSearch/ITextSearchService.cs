using ZWQ.TestCases.TextSearch.Models;

namespace ZWQ.TestCases.TextSearch;

/// <summary>
/// 大文件文本搜索服务接口。
/// 提供倒排索引构建、关键词搜索（含降级扫描和自适应学习）等功能。
/// </summary>
public interface ITextSearchService
{
    /// <summary>
    /// 构建指定文件的倒排索引（逐行流式读取 + Jieba 分词）
    /// </summary>
    /// <param name="filePath">要索引的文本文件路径</param>
    /// <param name="ct">取消令牌</param>
    Task BuildIndexAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// 搜索关键词。优先查倒排索引，未命中则降级全文扫描并自适应学习新词。
    /// </summary>
    /// <param name="keywords">要搜索的关键词列表</param>
    /// <param name="caseSensitive">是否区分大小写，默认 false</param>
    /// <param name="ct">取消令牌</param>
    Task<TextSearchSummary> SearchAsync(
        IEnumerable<string> keywords,
        bool caseSensitive = false,
        CancellationToken ct = default);

    /// <summary>
    /// 获取当前索引的统计信息
    /// </summary>
    TextSearchIndexStats GetStats();

    /// <summary>
    /// 索引是否已构建
    /// </summary>
    bool IsIndexBuilt { get; }

    /// <summary>
    /// 已索引的文件路径
    /// </summary>
    string? IndexedFilePath { get; }
}

/// <summary>
/// 索引统计信息
/// </summary>
public sealed record TextSearchIndexStats(
    string? FilePath,
    long FileSizeBytes,
    int LineCount,
    int WordCount,
    long TotalEntries,
    int LearnedWordCount,
    bool IsBuilt);
