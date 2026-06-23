using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.TextSearch;

// ─── 配置 ───
const string TestFile = @"D:\TestFiles\test_15mb.txt";
const string GroundTruthFile = @"D:\TestFiles\ground_truth.json";

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddTextSearch(config);

using var sp = services.BuildServiceProvider();
var searchService = sp.GetRequiredService<ITextSearchService>();

// ─── 检查测试文件 ───
if (!File.Exists(TestFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[错误] 测试文件不存在: {TestFile}");
    Console.ResetColor();
    return;
}

if (!File.Exists(GroundTruthFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[错误] 基准文件不存在: {GroundTruthFile}");
    Console.ResetColor();
    return;
}

// ─── 读取基准数据 ───
var groundTruthJson = await File.ReadAllTextAsync(GroundTruthFile);
var groundTruth = JsonSerializer.Deserialize<Dictionary<string, GroundTruthEntry>>(groundTruthJson)!;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("  ZWQ.TestCases.TextSearch — 15MB 中文文本搜索测试");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine($"测试文件: {TestFile}");
Console.WriteLine($"文件大小: {new FileInfo(TestFile).Length / (1024.0 * 1024):F2} MB");
Console.WriteLine($"基准文件: {GroundTruthFile}");
Console.WriteLine($"待测关键词: {groundTruth.Count} 个");
Console.WriteLine();

// ─── Step 1: 构建索引 ───
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("▸ Step 1: 构建倒排索引...");
Console.ResetColor();

var buildSw = Stopwatch.StartNew();
await searchService.BuildIndexAsync(TestFile);
buildSw.Stop();

var stats = searchService.GetStats();
Console.WriteLine();
Console.WriteLine($"  索引构建完成:");
Console.WriteLine($"    行数:       {stats.LineCount:N0}");
Console.WriteLine($"    总词数:     {stats.WordCount:N0}");
Console.WriteLine($"    不同词数:   {stats.TotalEntries:N0}");
Console.WriteLine($"    已学习词数: {stats.LearnedWordCount:N0}");
Console.WriteLine($"    耗时:       {buildSw.ElapsedMilliseconds:N0} ms ({buildSw.Elapsed.TotalSeconds:F1} s)");
Console.WriteLine();

// ─── Step 2: 搜索所有关键词 ───
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("▸ Step 2: 搜索关键词...");
Console.ResetColor();
Console.WriteLine();

var keywords = groundTruth.Keys.ToList();
var searchSw = Stopwatch.StartNew();
var summary = await searchService.SearchAsync(keywords);
searchSw.Stop();

Console.WriteLine($"  搜索耗时: {summary.SearchTimeMs} ms");
Console.WriteLine($"  总匹配数: {summary.TotalMatches:N0}");
Console.WriteLine();

// ─── Step 3: 逐项对比基准 ───
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("▸ Step 3: 对比基准数据...");
Console.ResetColor();
Console.WriteLine();

int passCount = 0;
int failCount = 0;
int posPassCount = 0;
int posFailCount = 0;

// 表头
Console.WriteLine($"  {"关键词",-14} {"预期",6} {"实际",7} {"差值",7} {"命中方式",-10} {"新学习",-6} {"前5位置",-8} {"结果",-4}");
Console.WriteLine($"  {new string('─', 80)}");

foreach (var result in summary.MatchResults)
{
    var keyword = result.Keyword;
    if (!groundTruth.TryGetValue(keyword, out var truth))
        continue;

    var actualCount = result.TotalCount;
    var expectedCount = truth.actual_count; // 用 actual_count 作为预期（包含基础段落中的出现）
    var diff = actualCount - expectedCount;
    var diffStr = diff == 0 ? "0" : (diff > 0 ? $"+{diff}" : $"{diff}");

    // 数量匹配判断（允许 ±5% 或 ±10 的误差，取较大值）
    var tolerance = Math.Max(10, (int)(expectedCount * 0.05));
    bool countMatch = Math.Abs(diff) <= tolerance;

    // 位置匹配判断
    bool posMatch = false;
    string posStr = "-";
    if (truth.first_5_positions != null && truth.first_5_positions.Count > 0 && result.Positions.Count >= truth.first_5_positions.Count)
    {
        int posHits = 0;
        for (int i = 0; i < truth.first_5_positions.Count; i++)
        {
            var expected = truth.first_5_positions[i];
            // 在所有结果中查找是否有匹配的位置（行号+列号）
            bool found = result.Positions.Any(p =>
                p.LineNumber == expected.line && p.ColumnIndex == expected.column);
            if (found) posHits++;
        }

        posMatch = posHits == truth.first_5_positions.Count;
        posStr = $"{posHits}/{truth.first_5_positions.Count}";
    }

    if (countMatch) passCount++; else failCount++;
    if (posMatch) posPassCount++; else posFailCount++;

    // 颜色
    Console.ForegroundColor = countMatch ? ConsoleColor.Green : ConsoleColor.Red;
    var countMark = countMatch ? "✓" : "✗";
    var posMark = posMatch ? "✓" : "✗";

    Console.Write($"  {keyword,-14}");
    Console.ResetColor();
    Console.Write($" {expectedCount,6} {actualCount,7} {diffStr,7}");

    Console.ForegroundColor = result.FoundViaFullTextScan ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
    Console.Write($" {(result.FoundViaFullTextScan ? "全文扫描" : "索引命中"),-10}");
    Console.ResetColor();

    Console.Write($" {(result.NewlyLearned ? "是" : "否"),-5}");
    Console.Write($" {posStr,-8}");

    Console.ForegroundColor = countMatch ? ConsoleColor.Green : ConsoleColor.Red;
    Console.Write($" {countMark}");
    Console.ResetColor();

    if (!posMatch && truth.first_5_positions != null)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($" (位置 {posMark})");
        Console.ResetColor();
    }

    Console.WriteLine();
}

Console.WriteLine($"  {new string('─', 80)}");

// ─── Step 4: 汇总 ───
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("▸ 测试结果汇总");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine($"  数量匹配: {passCount}/{passCount + failCount} ({(double)passCount / (passCount + failCount) * 100:F0}%)");
Console.WriteLine($"  位置匹配: {posPassCount}/{posPassCount + posFailCount} ({(double)posPassCount / (posPassCount + posFailCount) * 100:F0}%)");
Console.WriteLine();

// 打印索引最新统计
var finalStats = searchService.GetStats();
Console.WriteLine($"  索引最终状态:");
Console.WriteLine($"    不同词数:   {finalStats.WordCount:N0}");
Console.WriteLine($"    总条目数:   {finalStats.TotalEntries:N0}");
Console.WriteLine($"    已学习词数: {finalStats.LearnedWordCount:N0}");
Console.WriteLine();

// ─── Step 5: 第二轮搜索验证自适应学习 ───
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("▸ Step 4: 第二轮搜索（验证自适应学习效果）...");
Console.ResetColor();
Console.WriteLine();

var summary2 = await searchService.SearchAsync(keywords);

Console.WriteLine($"  第二轮搜索耗时: {summary2.SearchTimeMs} ms");
Console.WriteLine();

int learnedNowViaIndex = 0;
foreach (var r in summary2.MatchResults)
{
    if (!r.FoundViaFullTextScan)
        learnedNowViaIndex++;
}

Console.WriteLine($"  第二轮命中索引（非降级扫描）: {learnedNowViaIndex}/{summary2.MatchResults.Count}");
Console.WriteLine();

if (learnedNowViaIndex > 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ 自适应学习生效！第一轮通过全文扫描找到的词，第二轮已直接从索引命中。");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  注意: 第二轮仍有词未命中索引（可能因为词长度 < MinWordLengthForLearning）。");
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.ForegroundColor = failCount == 0 && posFailCount == 0
    ? ConsoleColor.Green : ConsoleColor.Yellow;
Console.WriteLine(failCount == 0 && posFailCount == 0
    ? "  全部测试通过！TextSearch 模块工作正常。"
    : $"  测试完成。数量未通过: {failCount}, 位置未通过: {posFailCount}");
Console.ResetColor();
Console.WriteLine("═══════════════════════════════════════════════════════════");

// ─── 基准数据结构 ───
class GroundTruthEntry
{
    public int expected_count { get; set; }
    public int actual_count { get; set; }
    public string? desc { get; set; }
    public List<GroundTruthPosition>? first_5_positions { get; set; }
}

class GroundTruthPosition
{
    public int line { get; set; }
    public int column { get; set; }
}
