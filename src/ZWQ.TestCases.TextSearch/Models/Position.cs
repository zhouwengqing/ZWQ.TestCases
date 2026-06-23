namespace ZWQ.TestCases.TextSearch.Models;

/// <summary>
/// 词语在文本中的位置信息
/// </summary>
public sealed record Position
{
    /// <summary>行号（从 1 开始）</summary>
    public int LineNumber { get; init; }

    /// <summary>列索引（从 0 开始，字符偏移）</summary>
    public int ColumnIndex { get; init; }

    /// <summary>词的长度（字符数）</summary>
    public int WordLength { get; init; }

    /// <summary>
    /// 用于去重比较的唯一键（行号 + 列索引）
    /// </summary>
    internal string DeduplicationKey => $"{LineNumber}:{ColumnIndex}";
}
