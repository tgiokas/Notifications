using Microsoft.AspNetCore.Mvc;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class SendGridWebhookController : ControllerBase
{
    private readonly ISendGridEventHandler _eventHandler;
    private readonly ILogger<SendGridWebhookController> _logger;

    public SendGridWebhookController(
        ISendGridEventHandler eventHandler,
        ILogger<SendGridWebhookController> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhooksEvents([FromBody] List<SendGridEventDto> events, CancellationToken cancellationToken)
    {
        if (events is null || events.Count == 0)
        {
            _logger.LogWarning("Received empty or null SendGrid webhook payload.");
            return Ok(); // Return 200 to avoid SendGrid retries
        }

        _logger.LogInformation("Received {Count} SendGrid event(s).", events.Count);

        try
        {
            await _eventHandler.HandleEventsAsync(events, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SendGrid webhook events.");
            // Still return 200 to avoid SendGrid retrying the same batch
        }

        return Ok();
    }
}
