using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

/// Entry point for sending emails over the REST API. Validates the request and
/// hands it to IEmailPublisher (OutboxEmailPublisher), which writes it to the
/// outbox table for OutboxProcessor to deliver. Kafka is not involved here —
/// KafkaEmailConsumer is a separate inbound path for external producers.
/// In a KAFKA_ENABLED=true deployment IEmailPublisher resolves to
/// DisabledEmailPublisher instead, since no outbox is configured there.
public class EmailService : IEmailService
{
    private readonly IEmailPublisher _emailPublisher;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IEmailPublisher emailPublisher, ILogger<EmailService> logger)
    {
        _emailPublisher = emailPublisher;
        _logger = logger;
    }

    public async Task<Result<string>> SendEmailAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(emailDto);
        if (validationError is not null)
            return Result<string>.Fail(validationError, "VALIDATION_ERROR");

        try
        {
            await _emailPublisher.PublishAsync(emailDto, cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Email rejected: REST delivery is disabled in this deployment.");
            return Result<string>.Fail(ex.Message, "REST_DISABLED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue email for delivery.");
            return Result<string>.Fail("Failed to queue email for delivery.", "PUBLISH_ERROR");
        }

        return Result<string>.Ok("Email queued for delivery.");
    }

    private static string? Validate(NotificationEmailDto emailDto)
    {
        if (!emailDto.HasAnyRecipients())
            return "At least one recipient (To, Cc, or Bcc) is required.";

        if (string.IsNullOrWhiteSpace(emailDto.Subject))
            return "Subject is required.";

        if (emailDto.Type is null && string.IsNullOrWhiteSpace(emailDto.Message))
            return "Either a template Type or a Message body is required.";

        return null;
    }
}
