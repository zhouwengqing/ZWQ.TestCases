using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ZWQ.TestCases.VectorSearch.Options;

namespace ZWQ.TestCases.VectorSearch.Embeddings;

/// <summary>
/// CLIP ONNX 推理服务 - 加载视觉/文本编码器并生成 512 维 Embedding 向量
/// </summary>
public sealed class ClipEmbeddingService : IClipEmbeddingService
{
    private readonly InferenceSession _visionSession;
    private readonly InferenceSession _textSession;
    private readonly ImagePreprocessor _preprocessor;
    private readonly BpeTokenizer _tokenizer;
    private readonly ClipModelOptions _options;
    private readonly ILogger<ClipEmbeddingService> _logger;

    public ClipEmbeddingService(
        IOptions<ClipModelOptions> options,
        ImagePreprocessor preprocessor,
        BpeTokenizer tokenizer,
        ILogger<ClipEmbeddingService> logger)
    {
        _options = options.Value;
        _preprocessor = preprocessor;
        _tokenizer = tokenizer;
        _logger = logger;

        if (!File.Exists(_options.VisionModelPath))
            throw new FileNotFoundException($"CLIP vision model not found: {_options.VisionModelPath}");
        if (!File.Exists(_options.TextModelPath))
            throw new FileNotFoundException($"CLIP text model not found: {_options.TextModelPath}");

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        _logger.LogInformation("[CLIP] Loading vision model: {Path}", _options.VisionModelPath);
        _visionSession = new InferenceSession(_options.VisionModelPath, sessionOptions);

        _logger.LogInformation("[CLIP] Loading text model: {Path}", _options.TextModelPath);
        _textSession = new InferenceSession(_options.TextModelPath, sessionOptions);

        _logger.LogInformation("[CLIP] Embedding service initialized (dim={Dim})", _options.EmbeddingDimension);
    }

    public Task<float[]> GetImageEmbeddingAsync(string imagePath, CancellationToken ct = default)
    {
        float[] pixelValues = _preprocessor.Preprocess(imagePath);
        var inputTensor = new DenseTensor<float>(pixelValues, [1, 3, 224, 224]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
        };

        using var results = _visionSession.Run(inputs);
        var outputTensor = results.First(r => r.Name == "image_embeds").AsTensor<float>();
        var embedding = outputTensor.ToArray();
        L2Normalize(embedding);

        return Task.FromResult(embedding);
    }

    public Task<float[][]> GetImageEmbeddingsBatchAsync(IReadOnlyList<string> imagePaths, CancellationToken ct = default)
    {
        int n = imagePaths.Count;
        float[] batchPixels = _preprocessor.PreprocessBatch(imagePaths);
        var inputTensor = new DenseTensor<float>(batchPixels, [n, 3, 224, 224]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
        };

        using var results = _visionSession.Run(inputs);
        var outputTensor = results.First(r => r.Name == "image_embeds").AsTensor<float>();
        var rawEmbeddings = outputTensor.ToArray();

        var embeddings = new float[n][];
        for (int i = 0; i < n; i++)
        {
            embeddings[i] = new float[_options.EmbeddingDimension];
            Array.Copy(rawEmbeddings, i * _options.EmbeddingDimension, embeddings[i], 0, _options.EmbeddingDimension);
            L2Normalize(embeddings[i]);
        }

        return Task.FromResult(embeddings);
    }

    public Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default)
    {
        int[] inputIds = _tokenizer.Encode(text);
        int[] attentionMask = _tokenizer.GetAttentionMask(text);

        var inputIdsTensor = new DenseTensor<int>(inputIds, [1, _options.MaxTokenLength]);
        var attentionMaskTensor = new DenseTensor<int>(attentionMask, [1, _options.MaxTokenLength]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = _textSession.Run(inputs);
        var outputTensor = results.First(r => r.Name == "text_embeds").AsTensor<float>();
        var embedding = outputTensor.ToArray();
        L2Normalize(embedding);

        return Task.FromResult(embedding);
    }

    private static void L2Normalize(float[] vector)
    {
        float norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm < 1e-12f) return;
        for (int i = 0; i < vector.Length; i++)
            vector[i] /= norm;
    }

    public void Dispose()
    {
        _visionSession.Dispose();
        _textSession.Dispose();
    }
}