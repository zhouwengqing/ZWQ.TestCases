using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ZWQ.TestCases.VectorSearch.Embeddings;

/// <summary>
/// CLIP 图像预处理器 - 将图片转换为 [1,3,224,224] 浮点张量
/// </summary>
public sealed class ImagePreprocessor
{
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];
    private const int TargetSize = 224;

    /// <summary>
    /// 预处理单张图片，返回 CHW 格式的浮点数组 [3,224,224] = 150528 个浮点数
    /// </summary>
    public float[] Preprocess(string imagePath)
    {
        using var image = Image.Load<Rgb24>(imagePath);

        int shortestSide = Math.Min(image.Width, image.Height);
        float scale = (float)TargetSize / shortestSide;
        int newWidth = (int)Math.Round(image.Width * scale);
        int newHeight = (int)Math.Round(image.Height * scale);
        image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Bicubic));

        int cropX = (newWidth - TargetSize) / 2;
        int cropY = (newHeight - TargetSize) / 2;
        image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, TargetSize, TargetSize)));

        var tensor = new float[3 * TargetSize * TargetSize];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < TargetSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < TargetSize; x++)
                {
                    var pixel = row[x];
                    int offset = y * TargetSize + x;
                    tensor[0 * TargetSize * TargetSize + offset] = (pixel.R / 255f - Mean[0]) / Std[0];
                    tensor[1 * TargetSize * TargetSize + offset] = (pixel.G / 255f - Mean[1]) / Std[1];
                    tensor[2 * TargetSize * TargetSize + offset] = (pixel.B / 255f - Mean[2]) / Std[2];
                }
            }
        });

        return tensor;
    }

    /// <summary>
    /// 批量预处理图片，返回连续内存的 [N,3,224,224] 浮点数组
    /// </summary>
    public float[] PreprocessBatch(IReadOnlyList<string> imagePaths)
    {
        int singleSize = 3 * TargetSize * TargetSize;
        var batch = new float[imagePaths.Count * singleSize];
        for (int i = 0; i < imagePaths.Count; i++)
        {
            var single = Preprocess(imagePaths[i]);
            Array.Copy(single, 0, batch, i * singleSize, singleSize);
        }
        return batch;
    }
}