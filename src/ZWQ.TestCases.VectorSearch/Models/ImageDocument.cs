namespace ZWQ.TestCases.VectorSearch.Models;

/// <summary>
/// 存储在 Qdrant 中的图片文档元数据（Payload）
/// </summary>
public sealed class ImageDocument
{
    /// <summary>图片完整路径</summary>
    public required string FilePath { get; init; }

    /// <summary>文件名</summary>
    public required string FileName { get; init; }

    /// <summary>文件大小（字节）</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>索引时间（UTC）</summary>
    public DateTime IndexedAtUtc { get; init; }

    /// <summary>文件内容 SHA256 哈希</summary>
    public string Sha256Hash { get; init; } = string.Empty;
}
