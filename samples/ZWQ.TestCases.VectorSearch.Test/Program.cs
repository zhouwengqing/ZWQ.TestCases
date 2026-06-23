using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Indexing;
using ZWQ.TestCases.VectorSearch.Options;
using ZWQ.TestCases.VectorSearch.Qdrant;
using ZWQ.TestCases.VectorSearch.Search;

// ====== 构建配置 ======
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

// 手动注册 VectorSearch（跳过 BackgroundService，避免自动索引 2 万张）
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
services.AddSingleton<IVectorIndexService, VectorIndexService>();
services.AddSingleton<IVectorSearchService, VectorSearchService>();

using var sp = services.BuildServiceProvider();

var indexService = sp.GetRequiredService<IVectorIndexService>();
var searchService = sp.GetRequiredService<IVectorSearchService>();
var logger = sp.GetRequiredService<ILogger<Program>>();

Console.WriteLine("==========================================");
Console.WriteLine("  ZWQ.TestCases.VectorSearch - 独立测试");
Console.WriteLine("==========================================\n");

// ====== Test 1: 索引 50 张图片 ======
Console.WriteLine("--- Test 1: 索引 50 张图片 ---");
var imageDir = config["VectorSearch:ImageDirectory"] ?? @"D:\Images";
var allImages = Directory.GetFiles(imageDir, "*.jpg", SearchOption.AllDirectories);
Console.WriteLine($"D:\\Images 中共有 {allImages.Length} 张图片");

var batch = allImages.Take(50).ToList();
try
{
    await indexService.IndexBatchAsync(batch);
    Console.WriteLine($"已索引 {batch.Count} 张图片!\n");
}
catch (Exception ex)
{
    Console.WriteLine($"索引失败: {ex.Message}\n");
    Console.WriteLine(ex.StackTrace);
    return;
}

// ====== Test 2: 以文搜图 ======
Console.WriteLine("--- Test 2: 以文搜图 ---");
string[] queries = { "a cat", "a dog", "a bird", "a car", "red", "blue" };

foreach (var q in queries)
{
    try
    {
        var results = await searchService.TextToImageSearchAsync(q, topK: 3);
        Console.WriteLine($"\"{q}\" => {results.Count} 个结果");
        foreach (var r in results)
            Console.WriteLine($"  [{r.Score:F4}] {r.Document.FileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  \"{q}\" 失败: {ex.Message}");
    }
}

// ====== Test 3: 以图搜图 ======
Console.WriteLine("\n--- Test 3: 以图搜图 ---");
var queryImg = batch[0];
Console.WriteLine($"查询图片: {Path.GetFileName(queryImg)}");
try
{
    var results = await searchService.ImageToImageSearchAsync(queryImg, topK: 5);
    Console.WriteLine($"结果: {results.Count} 个");
    foreach (var r in results)
        Console.WriteLine($"  [{r.Score:F4}] {r.Document.FileName}");
}
catch (Exception ex)
{
    Console.WriteLine($"以图搜图失败: {ex.Message}");
}

// ====== Test 4: 扩大索引到 200 张再搜索 ======
Console.WriteLine("\n--- Test 4: 索引 200 张后重新搜索 ---");
var moreImages = allImages.Skip(50).Take(200).ToList();
try
{
    await indexService.IndexBatchAsync(moreImages);
    Console.WriteLine($"已追加索引 {moreImages.Count} 张");
    
    var results = await searchService.TextToImageSearchAsync("a cat", topK: 5);
    Console.WriteLine($"\"a cat\" => {results.Count} 个结果");
    foreach (var r in results)
        Console.WriteLine($"  [{r.Score:F4}] {r.Document.FileName}");
}
catch (Exception ex)
{
    Console.WriteLine($"失败: {ex.Message}");
}

Console.WriteLine("\n==========================================");
Console.WriteLine("  测试完成!");
Console.WriteLine("==========================================");
