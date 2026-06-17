namespace ZWQ.TestCases.DesignPatterns.Models;

/// <summary>
/// 支付结果
/// </summary>
public class PaymentResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>交易流水号</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>使用的支付渠道</summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>支付金额</summary>
    public decimal Amount { get; set; }

    /// <summary>货币类型</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>支付渠道返回的原始消息</summary>
    public string ChannelMessage { get; set; } = string.Empty;

    /// <summary>处理时间（UTC）</summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>耗时（毫秒）</summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>创建成功结果</summary>
    public static PaymentResult Ok(PaymentMethod method, string transactionId, decimal amount, string currency, string message, long elapsedMs)
        => new()
        {
            Success = true,
            PaymentMethod = method,
            TransactionId = transactionId,
            Amount = amount,
            Currency = currency,
            ChannelMessage = message,
            ProcessedAt = DateTime.UtcNow,
            ElapsedMilliseconds = elapsedMs
        };

    /// <summary>创建失败结果</summary>
    public static PaymentResult Fail(PaymentMethod method, string message, long elapsedMs)
        => new()
        {
            Success = false,
            PaymentMethod = method,
            ChannelMessage = message,
            ProcessedAt = DateTime.UtcNow,
            ElapsedMilliseconds = elapsedMs
        };
}
