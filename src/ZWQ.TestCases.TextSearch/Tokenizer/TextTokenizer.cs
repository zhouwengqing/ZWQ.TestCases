using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using JiebaNet.Segmenter;

namespace ZWQ.TestCases.TextSearch.Tokenizer;

/// <summary>
/// 基于 Jieba.NET 的中文分词器。
/// 支持运行时动态添加新词（自适应学习），并持久化到用户词典文件。
/// </summary>
public sealed class TextTokenizer
{
    private readonly JiebaSegmenter _segmenter;
    private readonly string _userDictPath;
    private readonly ILogger<TextTokenizer> _logger;
    private readonly ConcurrentDictionary<string, byte> _learnedWords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 创建 TextTokenizer 实例，初始化 Jieba 分词器并加载用户词典
    /// </summary>
    /// <param name="userDictionaryPath">用户词典文件路径</param>
    /// <param name="logger">日志记录器</param>
    public TextTokenizer(string userDictionaryPath, ILogger<TextTokenizer> logger)
    {
        _userDictPath = userDictionaryPath;
        _logger = logger;

        EnsureUserDictFile();

        // Jieba.NET ConfigManager 用相对路径 "Resources\dict.txt" 查找词典，
        // 基于当前工作目录。VS / dotnet run 启动时工作目录是项目根目录，
        // 而 Resources 在 bin 输出目录。临时切换到程序集所在目录解决此问题。
        var originalDir = Environment.CurrentDirectory;
        var assemblyDir = AppContext.BaseDirectory;
        try
        {
            Environment.CurrentDirectory = assemblyDir;
            _segmenter = new JiebaSegmenter();

            // 加载用户词典（必须在创建 JiebaSegmenter 之后调用）
            _segmenter.LoadUserDict(_userDictPath);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
        }

        // 加载已有用户词典中的词到运行时集合
        LoadExistingUserDictWords();

        _logger.LogInformation("[分词器] Jieba.NET 分词器已加载，用户词典: {Path}", _userDictPath);
    }

    /// <summary>
    /// 对文本进行分词，返回词语及其在原文中的起始字符位置。
    /// 使用 Jieba 的 Tokenize API 直接获取精确位置。
    /// </summary>
    public IReadOnlyList<(string Word, int StartIndex)> Segment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<(string, int)>();

        // Tokenize 返回 Token { Word, StartIndex, EndIndex }
        var tokens = _segmenter.Tokenize(text, TokenizerMode.Default, hmm: true);
        var result = new List<(string, int)>();

        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token.Word))
                result.Add((token.Word, token.StartIndex));
        }

        return result;
    }

    /// <summary>
    /// 检查词语是否在 Jieba 词典中（内置词典 + 已学习的新词）
    /// </summary>
    public bool ContainsWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        // 优先检查运行时学习的新词
        if (_learnedWords.ContainsKey(word))
            return true;

        // 再检查 Jieba 内置词典
        return WordDictionary.Instance.ContainsWord(word);
    }

    /// <summary>
    /// 将新词添加到运行时词典和 Jieba 分词器的内存词典中
    /// </summary>
    public void AddWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        if (_learnedWords.TryAdd(word, 0))
        {
            // freq=3 默认词频, tag="n" 名词词性
            _segmenter.AddWord(word, 3, "n");
            _logger.LogInformation("[分词器] 新词已学习: {Word}", word);
        }
    }

    /// <summary>
    /// 将所有运行时学习到的新词持久化到用户词典文件
    /// </summary>
    public int SaveLearnedWords()
    {
        if (_learnedWords.IsEmpty)
            return 0;

        var existingLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(_userDictPath))
        {
            foreach (var line in File.ReadAllLines(_userDictPath))
            {
                var w = line.Split(' ')[0].Trim();
                if (!string.IsNullOrEmpty(w))
                    existingLines.Add(w);
            }
        }

        var newWords = _learnedWords.Keys.Where(w => !existingLines.Contains(w)).ToList();
        if (newWords.Count == 0)
            return 0;

        using var writer = new StreamWriter(_userDictPath, append: true);
        foreach (var word in newWords)
        {
            // Jieba 用户词典格式：词语 词频 词性
            writer.WriteLine($"{word} 3 n");
        }

        _logger.LogInformation("[分词器] 已将 {Count} 个新词持久化到用户词典", newWords.Count);
        return newWords.Count;
    }

    /// <summary>
    /// 获取运行时已学习的新词数量
    /// </summary>
    public int LearnedWordCount => _learnedWords.Count;

    private void EnsureUserDictFile()
    {
        var dir = Path.GetDirectoryName(_userDictPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_userDictPath))
        {
            File.WriteAllText(_userDictPath, string.Empty);
            _logger.LogInformation("[分词器] 已创建空用户词典文件: {Path}", _userDictPath);
        }
    }

    private void LoadExistingUserDictWords()
    {
        if (!File.Exists(_userDictPath))
            return;

        int count = 0;
        foreach (var line in File.ReadAllLines(_userDictPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var word = line.Split(' ')[0].Trim();
            if (!string.IsNullOrEmpty(word))
            {
                _learnedWords.TryAdd(word, 0);
                count++;
            }
        }

        if (count > 0)
            _logger.LogInformation("[分词器] 从用户词典加载了 {Count} 个词", count);
    }
}
