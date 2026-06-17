using ZWQ.TestCases.DesignPatterns.Models;
using ZWQ.TestCases.DesignPatterns.Strategy;

namespace ZWQ.TestCases.DesignPatterns.Factory;

/// <summary>
/// 支付策略工厂接口
/// 
/// 工厂模式负责根据支付方式类型创建/获取对应的策略实例，
/// 客户端无需知道具体策略类的存在。
/// </summary>
public interface IPaymentStrategyFactory
{
    /// <summary>
    /// 根据支付方式获取对应的策略实例
    /// </summary>
    /// <param name="method">支付方式</param>
    /// <returns>对应的支付策略</returns>
    /// <exception cref="NotSupportedException">不支持的支付方式</exception>
    IPaymentStrategy GetStrategy(PaymentMethod method);

    /// <summary>
    /// 获取所有已注册的支付方式
    /// </summary>
    IReadOnlyCollection<PaymentMethod> GetSupportedMethods();
}
