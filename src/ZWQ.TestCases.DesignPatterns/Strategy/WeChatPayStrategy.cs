using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.DesignPatterns.Strategy;

/// <summary>
/// 微信支付策略
/// 
/// 演示要点：
///   - 每个策略有独立的参数校验逻辑（微信支付需要 openId）
///   - 封装微信特有的签名、证书加载等逻辑
///   - 实际项目中这里会调用微信支付 V3 SDK（WeChatPayAPI）
/// </summary>
public class WeChatPayStrategy : IPaymentStrategy
{
    private readonly ILogger<WeChatPayStrategy> _logger;

    /// <inheritdoc />
    public PaymentMethod Method => PaymentMethod.WeChatPay;

    public WeChatPayStrategy(ILogger<WeChatPayStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentResult> PayAsync(PaymentRequest request)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[微信支付] 发起支付: 订单={OrderId}, 金额={Amount} {Currency}, 用户={UserId}",
            request.OrderId, request.Amount, request.Currency, request.UserId);

        // ====== 微信支付特有的校验 ======
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            sw.Stop();
            return PaymentResult.Fail(PaymentMethod.WeChatPay, "微信支付需要用户 openId（UserId 不能为空）", sw.ElapsedMilliseconds);
        }

        // ====== 模拟微信支付 V3 SDK 调用 ======
        // 实际项目中的代码：
        //   var config = new WeChatPayConfig { AppId = "...", MchId = "...", ApiV3Key = "...", PrivateKey = "..." };
        //   var client = new WeChatPayClient(config);
        //   var req = new JsApiRequest { OutTradeNo = request.OrderId, Total = (int)(request.Amount * 100), ... };
        //   var response = await client.ExecuteAsync(req);

        await Task.Delay(Random.Shared.Next(80, 250)); // 模拟网络延迟

        var transactionId = $"WX{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

        sw.Stop();
        _logger.LogInformation("[微信支付] 支付成功: 交易号={TxId}, 耗时={Elapsed}ms",
            transactionId, sw.ElapsedMilliseconds);

        return PaymentResult.Ok(
            PaymentMethod.WeChatPay, transactionId,
            request.Amount, request.Currency,
            "微信支付成功（模拟）", sw.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[微信支付] 发起退款: 交易号={TxId}, 退款金额={Amount}", transactionId, amount);
        await Task.Delay(Random.Shared.Next(50, 150));
        _logger.LogInformation("[微信支付] 退款成功: 交易号={TxId}", transactionId);
        return true;
    }
}
