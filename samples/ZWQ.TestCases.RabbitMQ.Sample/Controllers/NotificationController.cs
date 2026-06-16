using Microsoft.AspNetCore.Mvc;
using ZWQ.TestCases.RabbitMQ.Sample.Models;
using ZWQ.TestCases.RabbitMQ.Sample.Services;

namespace ZWQ.TestCases.RabbitMQ.Sample.Controllers;

/// <summary>
/// 通知接口 — 发送通知并发布 MQ 消息
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="notificationService">通知服务</param>
    public NotificationController(INotificationService notificationService) => _notificationService = notificationService;

    /// <summary>
    /// 发送通知并发布 MQ 消息
    /// </summary>
    /// <param name="request">发送通知请求（渠道、标题、正文、目标用户）</param>
    /// <returns>已创建的通知信息</returns>
    [HttpPost]
    public IActionResult SendNotification([FromBody] SendNotificationRequest request)
    {
        var notification = _notificationService.SendNotification(request);
        return Ok(new { message = "通知已发布到 MQ", notification });
    }
}
