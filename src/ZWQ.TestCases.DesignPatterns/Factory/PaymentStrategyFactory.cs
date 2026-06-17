using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.DesignPatterns.Models;
using ZWQ.TestCases.DesignPatterns.Strategy;

namespace ZWQ.TestCases.DesignPatterns.Factory;

/// <summary>
/// 支付策略工厂实现（基于依赖注入的工厂模式）
/// 
/// 设计说明：
///   传统工厂模式使用 switch/if-else 手动 new 对象，但这种方式违反开闭原则 —
///   每新增一种支付方式都要修改工厂代码。
/// 
///   本实现采用 .NET DI 容器驱动的工厂模式：
///   1. 所有策略通过 DI 注册为 IEnumerable&lt;IPaymentStrategy&gt;
///   2. 工厂在构造时自动收集所有策略，按 Method 属性建立字典索引
///   3. 新增支付方式只需新增策略类 + 注册 DI，工厂代码无需修改
/// 
/// 这是策略模式 + 工厂模式 + 依赖注入三者结合的最佳实践。
/// </summary>
public class PaymentStrategyFactory : IPaymentStrategyFactory
{
    private readonly Dictionary<PaymentMethod, IPaymentStrategy> _strategies;
    private readonly ILogger<PaymentStrategyFactory> _logger;

    public PaymentStrategyFactory(
        IEnumerable<IPaymentStrategy> strategies,
        ILogger<PaymentStrategyFactory> logger)
    {
        _logger = logger;

        // 自动收集所有注入的策略，按 Method 建立索引
        _strategies = strategies.ToDictionary(s => s.Method, s => s);

        _logger.LogInformation(
            "[PaymentFactory] 已注册 {Count} 种支付策略: {Methods}",
            _strategies.Count,
            string.Join(", ", _strategies.Keys));
    }

    /// <inheritdoc />
    public IPaymentStrategy GetStrategy(PaymentMethod method)
    {
        if (_strategies.TryGetValue(method, out var strategy))
        {
            _logger.LogDebug("[PaymentFactory] 获取策略: {Method} → {Type}", method, strategy.GetType().Name);
            return strategy;
        }

        _logger.LogError("[PaymentFactory] 不支持的支付方式: {Method}", method);
        throw new NotSupportedException($"不支持的支付方式: {method}，当前支持: {string.Join(", ", _strategies.Keys)}");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentMethod> GetSupportedMethods()
        => _strategies.Keys.ToList().AsReadOnly();
}
