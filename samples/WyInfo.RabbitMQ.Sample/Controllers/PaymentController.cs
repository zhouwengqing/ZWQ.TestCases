using Microsoft.AspNetCore.Mvc;
using WyInfo.RabbitMQ.Sample.Models;
using WyInfo.RabbitMQ.Sample.Services;

namespace WyInfo.RabbitMQ.Sample.Controllers;

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
