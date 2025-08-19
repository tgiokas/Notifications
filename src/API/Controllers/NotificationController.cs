using Microsoft.AspNetCore.Mvc;

using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationPublisher _publisher;

    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationPublisher publisher, ILogger<NotificationController> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("pong");

    [HttpPost("Send")]
    public async Task<IActionResult> SendMessage(KafkaMessage<NotificationRequestDto> message, CancellationToken cancellationToken)
    {
        try
        {
            if (message?.Content is null)
            {
                return BadRequest("Message content is required.");
            }

            await _publisher.PublishAsync(message.Content, cancellationToken);
            return Accepted(); 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Failed to send message to Kafka");
        }
    }

    [HttpPost("SendMessage2")]
    public async Task<IActionResult> SendMessage2(NotificationRequestDto message, CancellationToken cancellationToken)
    {
        try
        {
            if (message is null)
            {
                return BadRequest("Message content is required.");
            }

            await _publisher.PublishAsync(message, cancellationToken);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Failed to send message to Kafka");
        }
    }
}

