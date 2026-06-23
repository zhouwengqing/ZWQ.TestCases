using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Options;
using ZWQ.TestCases.VectorSearch.Qdrant;
using ZWQ.TestCases.VectorSearch.Search;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

services.Configure<VectorSearchOptions>(config.GetSection("VectorSearch"));
services.Configure<QdrantOptions>(config.GetSection("Qdrant"));
services.Configure<ClipModelOptions>(config.GetSection("ClipModel"));
services.AddSingleton<ImagePreprocessor>();
services.AddSingleton<BpeTokenizer>();
services.AddSingleton<IClipEmbeddingService, ClipEmbeddingService>();
services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
    return new QdrantClient(opts.Host, opts.GrpcPort, opts.UseHttps, opts.ApiKey);
});
services.AddSingleton<IQdrantCollectionManager, QdrantCollectionManager>();
services.AddSingleton<IVectorSearchService, VectorSearchService>();

using var sp = services.BuildServiceProvider();
var searchService = sp.GetRequiredService<IVectorSearchService>();

Console.WriteLine("==========================================");
Console.WriteLine("  Qdrant 20,000 张图片 — 搜索验证");
Console.WriteLine("==========================================\n");

// Text-to-Image tests
string[] queries = {
    "a cat", "a dog", "a bird", "a frog",
    "a car", "a flower", "a tree",
    "something red", "blue ocean", "green grass"
};

Console.WriteLine("--- 以文搜图 (Text → Image) ---\n");
foreach (var q in queries)
{
    var results = await searchService.TextToImageSearchAsync(q, topK: 5);
    Console.WriteLine($"\"{q}\" => top 5:");
    foreach (var r in results)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(r.Document.FilePath)) ?? "";
        Console.WriteLine($"  [{r.Score:F4}] {folder}/{r.Document.FileName}");
    }
    Console.WriteLine();
}

// Image-to-Image test
Console.WriteLine("--- 以图搜图 (Image → Image) ---\n");
var sampleImages = Directory.GetFiles(config["VectorSearch:ImageDirectory"] ?? @"D:\Images", "*.jpg", SearchOption.AllDirectories);
var queryImg = sampleImages[0];
Console.WriteLine($"查询图片: {queryImg}\n");
var imgResults = await searchService.ImageToImageSearchAsync(queryImg, topK: 10);
foreach (var r in imgResults)
{
    var folder = Path.GetFileName(Path.GetDirectoryName(r.Document.FilePath)) ?? "";
    Console.WriteLine($"  [{r.Score:F4}] {folder}/{r.Document.FileName}");
}

Console.WriteLine("\n==========================================");
Console.WriteLine("  验证完成!");
Console.WriteLine("==========================================");
