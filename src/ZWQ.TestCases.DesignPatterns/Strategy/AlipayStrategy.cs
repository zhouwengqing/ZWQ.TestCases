using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.DesignPatterns.Strategy;

/// <summary>
/// 支付宝支付策略
/// 
/// 演示要点：
///   - 实现 IPaymentStrategy 接口
///   - 封装支付宝特有的支付参数构建和调用逻辑
///   - 实际项目中这里会调用支付宝 SDK（Alipay.AopSdk）
/// </summary>
public class AlipayStrategy : IPaymentStrategy
{
    private readonly ILogger<AlipayStrategy> _logger;

    /// <inheritdoc />
    public PaymentMethod Method => PaymentMethod.Alipay;

    public AlipayStrategy(ILogger<AlipayStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentResult> PayAsync(PaymentRequest request)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[支付宝] 发起支付: 订单={OrderId}, 金额={Amount} {Currency}",
            request.OrderId, request.Amount, request.Currency);

        // ====== 模拟支付宝 SDK 调用 ======
        // 实际项目中的代码：
        //   var client = new DefaultAopClient(appId, privateKey, alipayPublicKey);
        //   var bizContent = new { out_trade_no = request.OrderId, total_amount = request.Amount, subject = request.Description };
        //   var alipayRequest = new AlipayTradePagePayRequest { BizContent = JsonSerializer.Serialize(bizContent) };
        //   var response = client.Execute(alipayRequest);

        await Task.Delay(Random.Shared.Next(100, 300)); // 模拟网络延迟

        var transactionId = $"ALI{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

        sw.Stop();
        _logger.LogInformation("[支付宝] 支付成功: 交易号={TxId}, 耗时={Elapsed}ms",
            transactionId, sw.ElapsedMilliseconds);

        return PaymentResult.Ok(
            PaymentMethod.Alipay, transactionId,
            request.Amount, request.Currency,
            "支付宝支付成功（模拟）", sw.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[支付宝] 发起退款: 交易号={TxId}, 退款金额={Amount}", transactionId, amount);
        await Task.Delay(Random.Shared.Next(50, 150));
        _logger.LogInformation("[支付宝] 退款成功: 交易号={TxId}", transactionId);
        return true;
    }
}
