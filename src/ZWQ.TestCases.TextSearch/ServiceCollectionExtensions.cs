using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZWQ.TestCases.TextSearch.Options;

namespace ZWQ.TestCases.TextSearch;

/// <summary>
/// 文本搜索模块的依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册文本搜索服务（从 IConfiguration 读取 TextSearch 配置节）
    /// </summary>
    public static IServiceCollection AddTextSearch(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TextSearchOptions>(configuration.GetSection("TextSearch"));
        services.AddSingleton<ITextSearchService, TextSearchService>();
        return services;
    }

    /// <summary>
    /// 注册文本搜索服务（使用默认配置）
    /// </summary>
    public static IServiceCollection AddTextSearch(
        this IServiceCollection services)
    {
        services.AddSingleton<ITextSearchService, TextSearchService>();
        return services;
    }
}
