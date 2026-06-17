using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.DesignPatterns.Factory;
using ZWQ.TestCases.DesignPatterns.Models;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 支付策略 API — 演示策略模式 + 工厂模式在支付场景中的应用
/// 
/// 核心思路：
///   1. 客户端只需指定 PaymentMethod（枚举），无需知道底层是支付宝还是微信
///   2. 工厂（IPaymentStrategyFactory）根据枚举值返回对应的策略实例
///   3. 策略实例执行各自的支付逻辑
///   4. 新增支付方式只需新增策略类，不修改已有代码（开闭原则）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PaymentStrategyController : ControllerBase
{
    private readonly IPaymentStrategyFactory _factory;
    private readonly ILogger<PaymentStrategyController> _logger;

    /// <summary>
    /// 构造函数 — 通过 DI 注入策略工厂
    /// </summary>
    public PaymentStrategyController(
        IPaymentStrategyFactory factory,
        ILogger<PaymentStrategyController> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// 发起支付 — 工厂模式 + 策略模式的核心入口
    /// 
    /// 客户端只需传入 PaymentMethod 枚举值，工厂自动选择对应的支付策略。
    /// 
    /// 支付流程：
    ///   1. 工厂根据 PaymentMethod 查找对应策略
    ///   2. 策略执行各自的支付逻辑（参数校验、调用第三方 API、返回结果）
    ///   3. 不同策略有不同的校验规则（如微信需要 UserId、PayPal 限制货币类型）
    /// </summary>
    /// <param name="request">支付请求</param>
    /// <returns>支付结果</returns>
    [HttpPost("pay")]
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pay([FromBody] PaymentRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest("支付金额必须大于 0");

        try
        {
            // 工厂根据支付方式获取对应策略
            var strategy = _factory.GetStrategy(request.PaymentMethod);

            _logger.LogInformation("开始支付: 订单={OrderId}, 渠道={Method}, 金额={Amount} {Currency}",
                request.OrderId, request.PaymentMethod, request.Amount, request.Currency);

            // 策略执行支付
            var result = await strategy.PayAsync(request);

            return Ok(result);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 发起退款
    /// </summary>
    /// <param name="paymentMethod">支付方式</param>
    /// <param name="transactionId">原交易流水号</param>
    /// <param name="amount">退款金额</param>
    /// <returns>退款结果</returns>
    [HttpPost("refund")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund(
        [FromQuery] PaymentMethod paymentMethod,
        [FromQuery] string transactionId,
        [FromQuery] decimal amount)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return BadRequest("交易流水号不能为空");

        if (amount <= 0)
            return BadRequest("退款金额必须大于 0");

        try
        {
            var strategy = _factory.GetStrategy(paymentMethod);
            var success = await strategy.RefundAsync(transactionId, amount);

            return Ok(new
            {
                Success = success,
                PaymentMethod = paymentMethod.ToString(),
                TransactionId = transactionId,
                RefundAmount = amount
            });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 查询所有支持的支付方式
    /// </summary>
    /// <returns>已注册的支付方式列表</returns>
    [HttpGet("methods")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetSupportedMethods()
    {
        var methods = _factory.GetSupportedMethods();
        return Ok(new
        {
            TotalCount = methods.Count,
            Methods = methods.Select(m => new
            {
                Name = m.ToString(),
                Value = (int)m
            })
        });
    }

    /// <summary>
    /// 批量支付演示 — 同一笔订单通过多种渠道支付（用于测试不同策略的行为差异）
    /// 
    /// 展示策略模式的优势：循环中只需切换枚举值，每个策略自动执行各自的逻辑。
    /// </summary>
    /// <param name="orderId">订单号</param>
    /// <param name="amount">金额</param>
    /// <param name="currency">货币</param>
    /// <returns>各渠道的支付结果</returns>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchPay(
        [FromQuery] string orderId,
        [FromQuery] decimal amount,
        [FromQuery] string currency = "CNY")
    {
        var methods = _factory.GetSupportedMethods();
        var results = new List<PaymentResult>();

        foreach (var method in methods)
        {
            var strategy = _factory.GetStrategy(method);
            var request = new PaymentRequest
            {
                OrderId = $"{orderId}_{method}",
                Amount = amount,
                Currency = currency,
                PaymentMethod = method,
                Description = $"批量测试 - {method}",
                UserId = "test_user_001"
            };

            var result = await strategy.PayAsync(request);
            results.Add(result);
        }

        return Ok(new
        {
            OrderId = orderId,
            TotalChannels = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailCount = results.Count(r => !r.Success),
            Results = results
        });
    }
}
