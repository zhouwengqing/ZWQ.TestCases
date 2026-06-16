using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.RabbitMQ.Sample.Models;
using ZWQ.TestCases.RabbitMQ.Sample.Services;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

    /// <summary>
    /// 创建支付并发布 MQ 消息
    /// </summary>
    [HttpPost]
    public IActionResult CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var payment = _paymentService.CreatePayment(request);
        return Ok(new { message = "支付已创建，MQ 消息已发布", payment });
    }
}
