using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

/// Entry point for sending emails synchronously over the REST API, as an
/// alternative to the Kafka consumer. Delegates the actual delivery to the
/// same IEmailSender used by KafkaEmailConsumer, so both paths share identical
/// provider, template and attachment handling.
public class EmailService : IEmailService
{
    private readonly IEmailSender _emailSender;

    public EmailService(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task<Result<string>> SendEmailAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(emailDto);
        if (validationError is not null)
            return Result<string>.Fail(validationError, "VALIDATION_ERROR");

        await _emailSender.SendAsync(emailDto, cancellationToken);

        return Result<string>.Ok("Email accepted for delivery.");
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
