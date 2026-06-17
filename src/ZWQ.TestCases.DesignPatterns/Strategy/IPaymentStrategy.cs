using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.DesignPatterns.Strategy;

/// <summary>
/// 支付策略接口（策略模式的核心抽象）
/// 
/// 每种支付方式对应一个独立的策略实现类，封装各自的支付逻辑。
/// 客户端通过 <see cref="Factory.IPaymentStrategyFactory"/> 获取具体策略，
/// 无需知道底层是支付宝、微信还是 PayPal。
/// </summary>
public interface IPaymentStrategy
{
    /// <summary>
    /// 该策略支持的支付方式
    /// </summary>
    PaymentMethod Method { get; }

    /// <summary>
    /// 执行支付
    /// </summary>
    /// <param name="request">支付请求</param>
    /// <returns>支付结果</returns>
    Task<PaymentResult> PayAsync(PaymentRequest request);

    /// <summary>
    /// 执行退款
    /// </summary>
    /// <param name="transactionId">原交易流水号</param>
    /// <param name="amount">退款金额</param>
    /// <returns>是否退款成功</returns>
    Task<bool> RefundAsync(string transactionId, decimal amount);
}
