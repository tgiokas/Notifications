using Microsoft.AspNetCore.Mvc;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.WebAPI.Controllers;

/// REST entry point for sending email: validates the request and queues it in
/// the outbox (via IEmailPublisher/EmailService) for OutboxProcessor to deliver
/// through IEmailSender (SendGrid/SMTP). Separate from, and unrelated to, the
/// Kafka pipeline: KafkaEmailConsumer keeps consuming external producers'
/// messages independently of this endpoint.
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
