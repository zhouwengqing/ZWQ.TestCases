using Microsoft.Extensions.DependencyInjection;
using ZWQ.TestCases.DesignPatterns.Factory;
using ZWQ.TestCases.DesignPatterns.Strategy;

namespace ZWQ.TestCases.DesignPatterns;

/// <summary>
/// 设计模式模块 DI 注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册策略模式 + 工厂模式相关服务
    /// 
    /// 注册内容：
    ///   - 所有支付策略（IPaymentStrategy 实现类）
    ///   - 支付策略工厂（IPaymentStrategyFactory）
    /// 
    /// 新增支付方式只需：
    ///   1. 创建新的策略类实现 IPaymentStrategy
    ///   2. 在此方法中添加一行 services.AddSingleton&lt;IPaymentStrategy, NewStrategy&gt;()
    /// </summary>
    public static IServiceCollection AddZwqDesignPatterns(this IServiceCollection services)
    {
        // ====== 注册所有支付策略 ======
        // 每种支付方式是一个独立的策略实现，全部注册到同一个接口下
        // DI 容器会以 IEnumerable<IPaymentStrategy> 的形式注入到工厂中
        services.AddSingleton<IPaymentStrategy, AlipayStrategy>();
        services.AddSingleton<IPaymentStrategy, WeChatPayStrategy>();
        services.AddSingleton<IPaymentStrategy, PayPalStrategy>();
        services.AddSingleton<IPaymentStrategy, CreditCardStrategy>();

        // ====== 注册工厂 ======
        // 工厂通过构造函数注入 IEnumerable<IPaymentStrategy> 自动收集所有策略
        services.AddSingleton<IPaymentStrategyFactory, PaymentStrategyFactory>();

        return services;
    }
}
