using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.RabbitMQ.Sample.Models;
using ZWQ.TestCases.RabbitMQ.Sample.Services;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 支付接口 — 创建支付并发布 MQ 消息
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="paymentService">支付服务</param>
    public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

    /// <summary>
    /// 创建支付并发布 MQ 消息
    /// </summary>
    /// <param name="request">创建支付请求（订单 ID、金额、支付方式）</param>
    /// <returns>已创建的支付信息</returns>
    [HttpPost]
    public IActionResult CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var payment = _paymentService.CreatePayment(request);
        return Ok(new { message = "支付已创建，MQ 消息已发布", payment });
    }
}
