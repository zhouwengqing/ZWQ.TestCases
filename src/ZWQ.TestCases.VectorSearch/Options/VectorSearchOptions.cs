namespace ZWQ.TestCases.VectorSearch.Options;

/// <summary>
/// 向量搜索模块全局配置
/// </summary>
public sealed class VectorSearchOptions
{
    /// <summary>
    /// 批量索引的默认图片目录
    /// </summary>
    public string ImageDirectory { get; set; } = @"D:\Images";

    /// <summary>
    /// 支持的图片文件扩展名
    /// </summary>
    public string[] SupportedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    /// <summary>
    /// ONNX 推理批量大小
    /// </summary>
    public int BatchSize { get; set; } = 16;
}
