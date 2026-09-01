using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

public interface IEmailService
{
    /// Validates the request and hands it off to the configured IEmailSender.
    Task<Result<string>> SendEmailAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default);
}
