using Microsoft.AspNetCore.Mvc;
using WyInfo.RabbitMQ.Sample.Models;
using WyInfo.RabbitMQ.Sample.Services;

namespace WyInfo.RabbitMQ.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService) => _notificationService = notificationService;

    /// <summary>
    /// 发送通知并发布 MQ 消息
    /// </summary>
    [HttpPost]
    public IActionResult SendNotification([FromBody] SendNotificationRequest request)
    {
        var notification = _notificationService.SendNotification(request);
        return Ok(new { message = "通知已发布到 MQ", notification });
    }
}
