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
            string htmlBody = emailDto.Type is null
                ? emailDto.Message ?? string.Empty
                : await _templateService.RenderAsync((EmailTemplateType)emailDto.Type, emailDto.TemplateParams ?? new Dictionary<string, string>());

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

            // UseDefaultCredentials must be set BEFORE Credentials — and must be false,
            // otherwise SmtpClient ignores the supplied NetworkCredential and tries to
            // submit as the (empty) process identity, producing "Client host rejected"
            // on relays that require AUTH (e.g. submission on port 587).
            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {                
                //UseDefaultCredentials = true,
                EnableSsl = true
            };

            // Only attach credentials when a username is configured. IP-allowlisted MX
            // endpoints (e.g. Exchange Online inbound connectors) accept unauthenticated
            // submissions; sending an empty AUTH LOGIN there would just confuse them.
            if (!string.IsNullOrWhiteSpace(_smtp.Username))
            {
                client.Credentials = new NetworkCredential(_smtp.Username, _smtp.Password);
            }

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