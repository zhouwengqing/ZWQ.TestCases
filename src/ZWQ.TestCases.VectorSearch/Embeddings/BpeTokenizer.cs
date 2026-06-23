using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZWQ.TestCases.VectorSearch.Options;

namespace ZWQ.TestCases.VectorSearch.Embeddings;

/// <summary>
/// CLIP BPE 分词器 - 将文本编码为 Token ID 序列
/// </summary>
public sealed partial class BpeTokenizer
{
    private const int StartOfText = 49406;
    private const int EndOfText = 49407;

    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<(string, string), int> _bpeRanks;
    private readonly int _maxLen;
    private readonly ILogger<BpeTokenizer> _logger;

    public BpeTokenizer(IOptions<ClipModelOptions> options, ILogger<BpeTokenizer> logger)
    {
        _maxLen = options.Value.MaxTokenLength;
        _logger = logger;

        var vocabPath = options.Value.VocabPath;
        var mergesPath = options.Value.MergesPath;

        if (!File.Exists(vocabPath))
            throw new FileNotFoundException($"CLIP vocab file not found: {vocabPath}");
        if (!File.Exists(mergesPath))
            throw new FileNotFoundException($"CLIP merges file not found: {mergesPath}");

        var vocabJson = File.ReadAllText(vocabPath);
        _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)!;

        var mergesLines = File.ReadAllLines(mergesPath);
        _bpeRanks = new Dictionary<(string, string), int>();
        int rank = 0;
        foreach (var line in mergesLines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(' ', 2);
            if (parts.Length == 2)
                _bpeRanks[(parts[0], parts[1])] = rank++;
        }

        _logger.LogInformation("[CLIP] BPE 分词器已加载: {VocabSize} 个词元, {MergeRules} 条合并规则",
            _vocab.Count, _bpeRanks.Count);
    }

    /// <summary>
    /// 将文本编码为固定长度 77 的 Token ID 数组（含 BOS/EOS，Padding 到 77）
    /// </summary>
    public int[] Encode(string text)
    {
        text = text.ToLowerInvariant().Trim();

        var tokens = ClipTokenizerRegex().Matches(text)
            .Select(m => m.Value)
            .ToList();

        var bpeTokens = new List<int> { StartOfText };
        foreach (var token in tokens)
        {
            foreach (var bpeToken in BpeEncode(token))
            {
                if (_vocab.TryGetValue(bpeToken, out int id))
                    bpeTokens.Add(id);
            }
        }
        bpeTokens.Add(EndOfText);

        if (bpeTokens.Count > _maxLen)
        {
            bpeTokens = bpeTokens.Take(_maxLen - 1).ToList();
            bpeTokens.Add(EndOfText);
        }

        while (bpeTokens.Count < _maxLen)
            bpeTokens.Add(0);

        return bpeTokens.ToArray();
    }

    /// <summary>
    /// 获取 Attention Mask（1=真实 Token，0=Padding）
    /// </summary>
    public int[] GetAttentionMask(string text)
    {
        var tokens = Encode(text);
        return tokens.Select(t => t != 0 ? 1 : 0).ToArray();
    }

    private List<string> BpeEncode(string token)
    {
        var word = new List<string>();
        for (int i = 0; i < token.Length; i++)
        {
            word.Add(i == token.Length - 1 ? token[i] + "</w>" : token[i].ToString());
        }

        while (word.Count > 1)
        {
            (string, string)? bestPair = null;
            int bestRank = int.MaxValue;

            for (int i = 0; i < word.Count - 1; i++)
            {
                var pair = (word[i], word[i + 1]);
                if (_bpeRanks.TryGetValue(pair, out int r) && r < bestRank)
                {
                    bestRank = r;
                    bestPair = pair;
                }
            }

            if (bestPair is null) break;

            var merged = new List<string>();
            for (int i = 0; i < word.Count; i++)
            {
                if (i < word.Count - 1 && word[i] == bestPair.Value.Item1 && word[i + 1] == bestPair.Value.Item2)
                {
                    merged.Add(bestPair.Value.Item1 + bestPair.Value.Item2);
                    i++;
                }
                else
                {
                    merged.Add(word[i]);
                }
            }
            word = merged;
        }

        return word;
    }

    [GeneratedRegex(@"<\|startoftext\|>|<\|endoftext\|>|'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+", RegexOptions.IgnoreCase)]
    private static partial Regex ClipTokenizerRegex();
}