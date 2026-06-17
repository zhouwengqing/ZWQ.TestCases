using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.DesignPatterns.Strategy;

/// <summary>
/// 银行卡/信用卡支付策略
/// 
/// 演示要点：
///   - 需要额外的卡号校验逻辑
///   - 实际项目中会对接银联、Stripe 等网关
/// </summary>
public class CreditCardStrategy : IPaymentStrategy
{
    private readonly ILogger<CreditCardStrategy> _logger;

    /// <inheritdoc />
    public PaymentMethod Method => PaymentMethod.CreditCard;

    public CreditCardStrategy(ILogger<CreditCardStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentResult> PayAsync(PaymentRequest request)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[银行卡] 发起支付: 订单={OrderId}, 金额={Amount} {Currency}",
            request.OrderId, request.Amount, request.Currency);

        // ====== 银行卡特有的金额校验 ======
        if (request.Amount <= 0)
        {
            sw.Stop();
            return PaymentResult.Fail(PaymentMethod.CreditCard, "支付金额必须大于 0", sw.ElapsedMilliseconds);
        }

        if (request.Amount > 50000)
        {
            sw.Stop();
            _logger.LogWarning("[银行卡] 单笔限额超出: {Amount}", request.Amount);
            return PaymentResult.Fail(PaymentMethod.CreditCard,
                "银行卡单笔限额 50,000 元，请改用其他支付方式", sw.ElapsedMilliseconds);
        }

        // ====== 模拟银联/Stripe 网关调用 ======
        // 实际项目中的代码：
        //   var gateway = new PaymentGateway(apiKey);
        //   var charge = await gateway.ChargeAsync(new ChargeRequest { Amount = request.Amount, ... });

        await Task.Delay(Random.Shared.Next(120, 350));

        var transactionId = $"CC{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

        sw.Stop();
        _logger.LogInformation("[银行卡] 支付成功: 交易号={TxId}, 耗时={Elapsed}ms",
            transactionId, sw.ElapsedMilliseconds);

        return PaymentResult.Ok(
            PaymentMethod.CreditCard, transactionId,
            request.Amount, request.Currency,
            "银行卡支付成功（模拟）", sw.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[银行卡] 发起退款: 交易号={TxId}, 退款金额={Amount}", transactionId, amount);
        await Task.Delay(Random.Shared.Next(80, 180));
        _logger.LogInformation("[银行卡] 退款成功: 交易号={TxId}", transactionId);
        return true;
    }
}
