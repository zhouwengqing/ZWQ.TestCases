using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZWQ.TestCases.TextSearch.Index;
using ZWQ.TestCases.TextSearch.Models;
using ZWQ.TestCases.TextSearch.Options;
using ZWQ.TestCases.TextSearch.Tokenizer;

namespace ZWQ.TestCases.TextSearch;

/// <summary>
/// 大文件文本搜索服务。
/// <para>核心流程：</para>
/// <list type="number">
///   <item>构建索引：逐行流式读取文件 → Jieba 分词 → 写入倒排索引</item>
///   <item>查询索引：倒排索引 O(1) 查找 → 命中即返回</item>
///   <item>降级扫描：索引未命中 → 全文逐行 IndexOf 扫描</item>
///   <item>自适应学习：扫描命中的词 → 回填倒排索引 + 加入 Jieba 词典 + 持久化</item>
/// </list>
/// </summary>
public sealed class TextSearchService : ITextSearchService, IDisposable
{
    private readonly TextTokenizer _tokenizer;
    private readonly InvertedIndex _index = new();
    private readonly TextSearchOptions _options;
    private readonly ILogger<TextSearchService> _logger;

    private string? _filePath;
    private int _lineCount;
    private long _fileSizeBytes;
    private bool _isIndexBuilt;

    /// <summary>
    /// 创建 TextSearchService 实例
    /// </summary>
    public TextSearchService(
        IOptions<TextSearchOptions> options,
        ILogger<TextSearchService> logger,
        ILogger<TextTokenizer> tokenizerLogger)
    {
        _options = options.Value;
        _logger = logger;
        _tokenizer = new TextTokenizer(_options.UserDictionaryPath, tokenizerLogger);
    }

    /// <inheritdoc />
    public bool IsIndexBuilt => _isIndexBuilt;

    /// <inheritdoc />
    public string? IndexedFilePath => _filePath;

    // ───────────────────────── 构建索引 ─────────────────────────

    /// <inheritdoc />
    public async Task BuildIndexAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("指定的文件不存在", filePath);

        var fullPath = Path.GetFullPath(filePath);
        var fi = new FileInfo(fullPath);
        _logger.LogInformation("[搜索] 开始构建倒排索引: {Path} ({Size:F2} MB)",
            fullPath, fi.Length / (1024.0 * 1024));

        var sw = Stopwatch.StartNew();

        // 重置状态
        _index.Clear();
        _filePath = fullPath;
        _fileSizeBytes = fi.Length;
        _lineCount = 0;

        int totalWords = 0;

        using var reader = new StreamReader(fullPath);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var segments = _tokenizer.Segment(line);
            foreach (var (word, startIndex) in segments)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;

                var position = new Position
                {
                    LineNumber = lineNumber,
                    ColumnIndex = startIndex,
                    WordLength = word.Length
                };

                _index.Add(word, position);
                totalWords++;
            }
        }

        sw.Stop();
        _lineCount = lineNumber;
        _isIndexBuilt = true;

        _logger.LogInformation(
            "[搜索] 倒排索引构建完成: {Lines} 行, {Words} 个词, {Unique} 个不同词, 耗时 {Ms} ms",
            lineNumber, totalWords, _index.WordCount, sw.ElapsedMilliseconds);
    }

    // ───────────────────────── 搜索 ─────────────────────────

    /// <inheritdoc />
    public async Task<TextSearchSummary> SearchAsync(
        IEnumerable<string> keywords,
        bool caseSensitive = false,
        CancellationToken ct = default)
    {
        if (!_isIndexBuilt || _filePath == null)
            throw new InvalidOperationException("请先调用 BuildIndexAsync 构建索引");

        var sw = Stopwatch.StartNew();
        var keywordList = keywords.ToList();
        var results = new List<MatchResult>(keywordList.Count);

        foreach (var keyword in keywordList)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            var result = await SearchSingleAsync(keyword, caseSensitive, ct);
            results.Add(result);
        }

        sw.Stop();

        var summary = new TextSearchSummary
        {
            Keywords = keywordList,
            MatchResults = results,
            SearchTimeMs = sw.ElapsedMilliseconds,
            IndexedFilePath = _filePath
        };

        _logger.LogInformation(
            "[搜索] 搜索完成: {Keywords} 个关键词, 共 {Matches} 次匹配, 降级扫描={Scan}, 新学习={Learned}, 耗时 {Ms} ms",
            keywordList.Count, summary.TotalMatches, summary.UsedFullTextScan, summary.LearnedNewWord, sw.ElapsedMilliseconds);

        return summary;
    }

    // ───────────────────────── 统计 ─────────────────────────

    /// <inheritdoc />
    public TextSearchIndexStats GetStats() => new(
        FilePath: _filePath,
        FileSizeBytes: _fileSizeBytes,
        LineCount: _lineCount,
        WordCount: _index.WordCount,
        TotalEntries: _index.TotalEntries,
        LearnedWordCount: _tokenizer.LearnedWordCount,
        IsBuilt: _isIndexBuilt);

    /// <summary>
    /// 释放资源，退出前持久化学习到的新词到用户词典
    /// </summary>
    public void Dispose()
    {
        // 退出前持久化学习到的新词
        try
        {
            int saved = _tokenizer.SaveLearnedWords();
            if (saved > 0)
                _logger.LogInformation("[搜索] 退出前持久化了 {Count} 个新词", saved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[搜索] 持久化用户词典时出错");
        }
    }

    // ───────────────────────── 内部方法 ─────────────────────────

    private async Task<MatchResult> SearchSingleAsync(string keyword, bool caseSensitive, CancellationToken ct)
    {
        // Step 1: 倒排索引查找
        var positions = _index.Lookup(keyword);

        if (positions is { Count: > 0 })
        {
            // 如果需要区分大小写，对结果做二次过滤
            if (caseSensitive)
                positions = await FilterCaseSensitiveAsync(positions, keyword, ct);

            if (positions.Count > 0)
            {
                _logger.LogDebug("[搜索] 索引命中: '{Word}', {Count} 处", keyword, positions.Count);
                return new MatchResult
                {
                    Keyword = keyword,
                    Positions = positions,
                    FoundViaFullTextScan = false,
                    NewlyLearned = false
                };
            }
        }

        // Step 2: 降级 - 全文逐行扫描
        _logger.LogDebug("[搜索] 索引未命中, 降级全文扫描: '{Word}'", keyword);
        var scanPositions = await FullTextScanAsync(keyword, caseSensitive, ct);

        if (scanPositions.Count == 0)
        {
            _logger.LogDebug("[搜索] 全文扫描未找到: '{Word}'", keyword);
            return new MatchResult
            {
                Keyword = keyword,
                Positions = Array.Empty<Position>(),
                FoundViaFullTextScan = true,
                NewlyLearned = false
            };
        }

        // Step 3: 自适应学习
        bool learned = false;
        if (keyword.Length >= _options.MinWordLengthForLearning)
        {
            // 回填倒排索引
            _index.AddRange(keyword, scanPositions);

            // 加入 Jieba 运行时词典
            if (!_tokenizer.ContainsWord(keyword))
            {
                _tokenizer.AddWord(keyword);
                learned = true;
            }

            _logger.LogInformation(
                "[搜索] 自适应学习: '{Word}' 已加入词典和索引 ({Count} 处)",
                keyword, scanPositions.Count);
        }

        return new MatchResult
        {
            Keyword = keyword,
            Positions = scanPositions,
            FoundViaFullTextScan = true,
            NewlyLearned = learned
        };
    }

    /// <summary>
    /// 全文逐行扫描：用 IndexOf 在每一行中查找关键词的所有出现位置。
    /// 同时处理跨行匹配（关键词横跨两行边界的情况）。
    /// </summary>
    private async Task<List<Position>> FullTextScanAsync(
        string keyword, bool caseSensitive, CancellationToken ct)
    {
        var positions = new List<Position>();
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        using var reader = new StreamReader(_filePath!);
        string? line;
        int lineNumber = 0;
        string? prevLine = null;
        int prevLineNumber = 0;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            ct.ThrowIfCancellationRequested();

            // ── 行内搜索 ──
            int idx = 0;
            while ((idx = line.IndexOf(keyword, idx, comparison)) >= 0)
            {
                positions.Add(new Position
                {
                    LineNumber = lineNumber,
                    ColumnIndex = idx,
                    WordLength = keyword.Length
                });
                idx += keyword.Length;
            }

            // ── 跨行搜索：上一行尾部 + 当前行头部 ──
            if (prevLine != null && keyword.Length >= 2)
            {
                // 取上一行末尾的 keyword.Length-1 个字符（不够则取全部）
                int tailLen = Math.Min(keyword.Length - 1, prevLine.Length);
                // 取当前行开头的 keyword.Length-1 个字符（不够则取全部）
                int headLen = Math.Min(keyword.Length - 1, line.Length);

                if (tailLen > 0 && headLen > 0)
                {
                    // 拼接跨区域文本并搜索
                    var combined = string.Concat(
                        prevLine.AsSpan(prevLine.Length - tailLen),
                        line.AsSpan(0, headLen));

                    int searchStart = 0;
                    // 排除完全落在上一行内部的匹配（已在上一轮行内搜索中找到）
                    int skipUntil = Math.Max(0, tailLen - (keyword.Length - 1));

                    int ci = skipUntil;
                    while ((ci = combined.IndexOf(keyword, ci, comparison)) >= 0
                           && ci + keyword.Length <= combined.Length)
                    {
                        if (ci >= searchStart)
                        {
                            if (ci < tailLen)
                            {
                                // 匹配起始于上一行 → 报告到上一行
                                positions.Add(new Position
                                {
                                    LineNumber = prevLineNumber,
                                    ColumnIndex = prevLine.Length - tailLen + ci,
                                    WordLength = keyword.Length
                                });
                            }
                            else
                            {
                                // 匹配起始于当前行 → 报告到当前行
                                positions.Add(new Position
                                {
                                    LineNumber = lineNumber,
                                    ColumnIndex = ci - tailLen,
                                    WordLength = keyword.Length
                                });
                            }
                        }
                        ci++;
                    }
                }
            }

            prevLine = line;
            prevLineNumber = lineNumber;
        }

        return positions;
    }

    /// <summary>
    /// 区分大小写的二次过滤：回到原文验证每个位置的文本是否与关键词精确匹配
    /// </summary>
    private async Task<IReadOnlyList<Position>> FilterCaseSensitiveAsync(
        IReadOnlyList<Position> positions, string keyword, CancellationToken ct)
    {
        // 收集所有涉及的行号，避免重复读取
        var lineNumbers = positions.Select(p => p.LineNumber).Distinct().OrderBy(n => n).ToList();
        var lineCache = new Dictionary<int, string>();

        using var reader = new StreamReader(_filePath!);
        string? line;
        int currentLine = 0;
        int targetIdx = 0;

        while (targetIdx < lineNumbers.Count && (line = await reader.ReadLineAsync(ct)) != null)
        {
            currentLine++;
            if (currentLine == lineNumbers[targetIdx])
            {
                lineCache[currentLine] = line;
                targetIdx++;
            }
        }

        var filtered = new List<Position>();
        foreach (var pos in positions)
        {
            if (lineCache.TryGetValue(pos.LineNumber, out var lineText))
            {
                if (pos.ColumnIndex + keyword.Length <= lineText.Length)
                {
                    var actual = lineText.Substring(pos.ColumnIndex, keyword.Length);
                    if (string.Equals(actual, keyword, StringComparison.Ordinal))
                        filtered.Add(pos);
                }
            }
        }

        return filtered;
    }
}
