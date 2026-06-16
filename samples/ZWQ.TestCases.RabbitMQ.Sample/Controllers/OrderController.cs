using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.RabbitMQ.Sample.Models;
using ZWQ.TestCases.RabbitMQ.Sample.Services;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService) => _orderService = orderService;

    /// <summary>
    /// 创建订单并发布 MQ 消息
    /// </summary>
    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = _orderService.CreateOrder(request);
        return Ok(new { message = "订单已创建，MQ 消息已发布", order });
    }

    /// <summary>
    /// 批量创建订单（测试高并发场景）
    /// </summary>
    [HttpPost("batch")]
    public IActionResult BatchCreateOrders([FromQuery] int count = 10)
    {
        var orders = new List<OrderSubmittedEvent>();
        for (int i = 0; i < count; i++)
        {
            orders.Add(_orderService.CreateOrder(new CreateOrderRequest
            {
                CustomerEmail = $"user{i}@test.com",
                Amount = 50 + i * 10,
                Items = new() { $"批量商品-{i}" }
            }));
        }
        return Ok(new { message = $"批量创建 {count} 个订单完成", orders });
    }
}
