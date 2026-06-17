namespace ZWQ.TestCases.DesignPatterns.Models;

/// <summary>
/// 支付请求
/// </summary>
public class PaymentRequest
{
    /// <summary>订单号</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>支付金额（单位：元）</summary>
    public decimal Amount { get; set; }

    /// <summary>货币类型（CNY / USD / EUR 等）</summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>支付方式</summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>商品描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>用户 ID</summary>
    public string UserId { get; set; } = string.Empty;
}
