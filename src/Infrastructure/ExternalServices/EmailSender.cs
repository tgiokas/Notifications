using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Configuration;

namespace Notifications.Infrastructure.ExternalServices;

public class EmailSender : IEmailSender
{
    private readonly ITemplateService _templateService;
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ITemplateService templateService, IOptions<SmtpSettings> settings, ILogger<EmailSender> logger)
    {
        _templateService = templateService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                UseDefaultCredentials = true
            };

            string htmlBody = string.Empty;
            if (dto.TemplateParams is null && !string.IsNullOrEmpty(dto.Message))
                htmlBody = dto.Message;
            else
                htmlBody = await _templateService.RenderAsync(dto.Type ?? EmailTemplateType.Generic, dto.TemplateParams ?? new Dictionary<string, string>());

            var message = new MailMessage
            {
                From = new MailAddress(_settings.From),
                Subject = dto.Subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(dto.Recipient));

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent to {Recipient}", dto.Recipient);
        }
        catch (SmtpFailedRecipientException ex)
        {
            _logger.LogError(ex, "Failed recipient {Recipient}. Response: {StatusCode}", dto.Recipient, ex.StatusCode);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending to {Recipient}: {Message}", dto.Recipient, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending to {Recipient}", dto.Recipient);
        }
    }
}
