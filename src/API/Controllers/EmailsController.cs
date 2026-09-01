using Microsoft.AspNetCore.Mvc;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.WebAPI.Controllers;

/// REST entry point into the email pipeline: validates the request and queues
/// it for delivery via IEmailPublisher. Which queue that is — Kafka or a local
/// outbox — is chosen by EMAIL_DELIVERY_MODE and is transparent here; either
/// way the message ends up going through the same IEmailSender (SendGrid/SMTP)
/// used by every other producer.
[ApiController]
[Route("[controller]")]
public class EmailsController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailsController> _logger;

    public EmailsController(IEmailService emailService, ILogger<EmailsController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(Result<string>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Result<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send([FromBody] NotificationEmailDto request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(Result<string>.Fail("Request body is required.", "VALIDATION_ERROR"));

        var result = await _emailService.SendEmailAsync(request, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Email request rejected: {Message}", result.Message);
            return result.ErrorCode == "PUBLISH_ERROR"
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, result)
                : BadRequest(result);
        }

        _logger.LogInformation("Email queued for delivery. To: [{Recipients}]",
            string.Join(", ", request.GetAllToRecipients()));

        return Accepted(result);
    }
}
