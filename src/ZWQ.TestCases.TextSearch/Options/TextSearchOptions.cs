namespace ZWQ.TestCases.TextSearch.Options;

/// <summary>
/// 文本搜索模块配置项
/// </summary>
public sealed class TextSearchOptions
{
    /// <summary>
    /// Jieba 用户词典文件路径（用于持久化自适应学习的新词）。
    /// 默认为应用程序目录下的 textsearch_user_dict.txt。
    /// </summary>
    public string UserDictionaryPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "textsearch_user_dict.txt");

    /// <summary>
    /// 新词学习的最小长度（字符数），默认 2。
    /// 避免将单字符或无意义的短字符串加入词典。
    /// </summary>
    public int MinWordLengthForLearning { get; set; } = 2;
}
