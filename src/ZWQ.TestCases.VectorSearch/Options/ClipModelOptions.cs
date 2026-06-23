namespace ZWQ.TestCases.VectorSearch.Options;

/// <summary>
/// CLIP 模型文件路径与推理参数配置
/// </summary>
public sealed class ClipModelOptions
{
    /// <summary>
    /// ONNX 模型文件所在根目录
    /// </summary>
    public string ModelDirectory { get; set; } = @"D:\SW\Tools\clip-onnx";

    /// <summary>
    /// 视觉编码器文件名
    /// </summary>
    public string VisionModelFileName { get; set; } = "model_vision.onnx";

    /// <summary>
    /// 文本编码器文件名
    /// </summary>
    public string TextModelFileName { get; set; } = "model_text.onnx";

    /// <summary>
    /// BPE 词典文件名
    /// </summary>
    public string VocabFileName { get; set; } = "vocab.json";

    /// <summary>
    /// BPE 合并规则文件名
    /// </summary>
    public string MergesFileName { get; set; } = "merges.txt";

    /// <summary>
    /// CLIP 文本编码器最大 Token 长度
    /// </summary>
    public int MaxTokenLength { get; set; } = 77;

    /// <summary>
    /// CLIP ViT-B/32 向量维度
    /// </summary>
    public int EmbeddingDimension { get; set; } = 512;

    /// <summary>视觉模型完整路径</summary>
    public string VisionModelPath => Path.Combine(ModelDirectory, VisionModelFileName);

    /// <summary>文本模型完整路径</summary>
    public string TextModelPath => Path.Combine(ModelDirectory, TextModelFileName);

    /// <summary>词典完整路径</summary>
    public string VocabPath => Path.Combine(ModelDirectory, VocabFileName);

    /// <summary>合并规则完整路径</summary>
    public string MergesPath => Path.Combine(ModelDirectory, MergesFileName);
}
