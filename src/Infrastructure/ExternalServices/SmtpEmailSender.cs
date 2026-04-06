using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class SmtpEmailSender : IEmailSender
{
    private readonly ITemplateService _templateService;
    private readonly SmtpSettings _smtp;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        ITemplateService templateService,
        IOptions<EmailSettings> emailSettings,
        ILogger<SmtpEmailSender> logger)
    {
        _templateService = templateService;
        _smtp = emailSettings.Value.Smtp;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var from = !string.IsNullOrWhiteSpace(emailDto.Sender)
                ? emailDto.Sender
                : _smtp.From;

            _logger.LogInformation("Constructing SMTP Email...");

            // Render HTML body
            string htmlBody = emailDto.TemplateParams is null && !string.IsNullOrEmpty(emailDto.Message)
                ? emailDto.Message
                : await _templateService.RenderAsync(
                    emailDto.Type ?? EmailTemplateType.Generic,
                    emailDto.TemplateParams ?? new Dictionary<string, string>());

            var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = emailDto.Subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            var toAddresses = emailDto.GetAllToRecipients().ToList();
            if (toAddresses.Count == 0)
                throw new ArgumentException("At least one recipient is required.");

            foreach (var to in toAddresses)
                message.To.Add(new MailAddress(to));

            if (emailDto.Cc is { Count: > 0 })
                foreach (var cc in emailDto.Cc)
                    message.CC.Add(new MailAddress(cc));

            if (emailDto.Bcc is { Count: > 0 })
                foreach (var bcc in emailDto.Bcc)
                    message.Bcc.Add(new MailAddress(bcc));

            if (emailDto.ReplyTo is { Count: > 0 })
                foreach (var r in emailDto.ReplyTo)
                    message.ReplyToList.Add(new MailAddress(r));

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
                //UseDefaultCredentials = true,
                EnableSsl = true
            };

            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SMTP email sent to {Recipients}.",
                string.Join(", ", toAddresses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending via SMTP to {Recipient}", emailDto.Recipient);
        }
    }
}