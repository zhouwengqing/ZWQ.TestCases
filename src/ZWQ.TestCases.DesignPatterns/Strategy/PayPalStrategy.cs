using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.DesignPatterns.Strategy;

/// <summary>
/// PayPal 支付策略
/// 
/// 演示要点：
///   - 海外支付渠道，通常需要 USD 货币
///   - 封装 PayPal REST API 调用逻辑
///   - 实际项目中这里会调用 PayPal Checkout SDK
/// </summary>
public class PayPalStrategy : IPaymentStrategy
{
    private readonly ILogger<PayPalStrategy> _logger;

    /// <inheritdoc />
    public PaymentMethod Method => PaymentMethod.PayPal;

    public PayPalStrategy(ILogger<PayPalStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentResult> PayAsync(PaymentRequest request)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[PayPal] 发起支付: 订单={OrderId}, 金额={Amount} {Currency}",
            request.OrderId, request.Amount, request.Currency);

        // ====== PayPal 特有的货币校验 ======
        var supportedCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD" };
        if (!supportedCurrencies.Contains(request.Currency.ToUpperInvariant()))
        {
            sw.Stop();
            _logger.LogWarning("[PayPal] 不支持的货币类型: {Currency}", request.Currency);
            return PaymentResult.Fail(PaymentMethod.PayPal,
                $"PayPal 不支持货币 {request.Currency}，请使用 {string.Join("/", supportedCurrencies)}",
                sw.ElapsedMilliseconds);
        }

        // ====== 模拟 PayPal Checkout SDK 调用 ======
        // 实际项目中的代码：
        //   var environment = new SandboxEnvironment(clientId, clientSecret);
        //   var client = new PayPalHttpClient(environment);
        //   var createOrderRequest = new OrdersCreateRequest { ... };
        //   var response = await client.Execute(createOrderRequest);

        await Task.Delay(Random.Shared.Next(150, 400)); // PayPal 国际网络延迟更大

        var transactionId = $"PP{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

        sw.Stop();
        _logger.LogInformation("[PayPal] 支付成功: 交易号={TxId}, 耗时={Elapsed}ms",
            transactionId, sw.ElapsedMilliseconds);

        return PaymentResult.Ok(
            PaymentMethod.PayPal, transactionId,
            request.Amount, request.Currency,
            "PayPal payment completed (simulated)", sw.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[PayPal] 发起退款: 交易号={TxId}, 退款金额={Amount}", transactionId, amount);
        await Task.Delay(Random.Shared.Next(100, 200));
        _logger.LogInformation("[PayPal] 退款成功: 交易号={TxId}", transactionId);
        return true;
    }
}
