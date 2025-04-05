using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Notifications.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationPublisher _publisher;

    public NotificationsController(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] NotificationRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _publisher.PublishAsync(dto, cancellationToken);
            return Accepted(); // 202 Accepted → Queued for processing
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
