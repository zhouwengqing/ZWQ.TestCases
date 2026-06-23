using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using ZWQ.TestCases.VectorSearch.Embeddings;
using ZWQ.TestCases.VectorSearch.Indexing;
using ZWQ.TestCases.VectorSearch.Options;
using ZWQ.TestCases.VectorSearch.Qdrant;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

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

using var sp = services.BuildServiceProvider();
var indexService = sp.GetRequiredService<IVectorIndexService>();

Console.WriteLine("=== 测试去重逻辑 ===");
var sw = System.Diagnostics.Stopwatch.StartNew();

var imageDir = config["VectorSearch:ImageDirectory"] ?? @"D:\Images";
int count = await indexService.IndexDirectoryAsync(imageDir);

sw.Stop();
Console.WriteLine($"\n返回索引数: {count}");
Console.WriteLine($"耗时: {sw.Elapsed.TotalSeconds:F1} 秒");
Console.WriteLine(count == 0 ? "去重成功！没有重复索引。" : $"索引了 {count} 张新图片。");
