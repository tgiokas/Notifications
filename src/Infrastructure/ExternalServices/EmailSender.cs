using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Configuration;

namespace Notifications.Infrastructure.ExternalServices;

public class EmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<SmtpSettings> settings, ILogger<EmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(NotificationRequestDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {                
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                //UseDefaultCredentials = true,
                EnableSsl = _settings.UseSsl,
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.From),
                Subject = dto.Subject,
                Body = dto.Message,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(dto.Recipient));

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent to {Recipient}", dto.Recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", dto.Recipient);
            throw;
        }
    }
}
