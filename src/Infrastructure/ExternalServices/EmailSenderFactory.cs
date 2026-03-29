using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.ExternalServices;

public class EmailSenderFactory : IEmailSender
{
    private readonly IEmailSender _emailSender;

    public EmailSenderFactory(
        IConfiguration configuration,
        IEmailTemplateService templateService,
        ILogger<SmtpEmailSender> smtpLogger,
        ILogger<SendGridEmailSender> sendGridLogger)
    {
        var provider = configuration["EMAIL_PROVIDER"]?.Trim().ToLowerInvariant() ?? "smtp";

        _emailSender = provider switch
        {
            "sendgrid" => new SendGridEmailSender(
                templateService,
                configuration,
                sendGridLogger),

            "smtp" or _ => new SmtpEmailSender(
                templateService,
                configuration,
                smtpLogger),
        };
    }

    public Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        return _emailSender.SendAsync(emailDto, cancellationToken);
    }    
}
