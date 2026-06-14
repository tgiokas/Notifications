using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class SmtpEmailSender : IEmailSender
{
    private readonly ITemplateService _templateService;
    private readonly IAttachmentResolver _attachmentResolver;
    private readonly SmtpSettings _smtp;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        ITemplateService templateService,
        IAttachmentResolver attachmentResolver,
        IOptions<EmailSettings> emailSettings,
        ILogger<SmtpEmailSender> logger)
    {
        _templateService = templateService;
        _attachmentResolver = attachmentResolver;
        _smtp = emailSettings.Value.Smtp;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ResolvedAttachment> attachments = Array.Empty<ResolvedAttachment>();

        try
        {
            // Resolve attachments first. If any is too large or unavailable, this throws and
            // the whole email is dropped (caught below) before we touch the SMTP server.
            attachments = await _attachmentResolver.ResolveAsync(emailDto.Attachments, cancellationToken);

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

            // Attach resolved files.
            foreach (var att in attachments)
                message.Attachments.Add(new Attachment(att.Content, att.FileName, att.ContentType));

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
        catch (AttachmentTooLargeException ex)
        {
            // Drop the whole email, do not send a partial message.
            _logger.LogError(ex, "Email to {Recipient} not sent: attachments exceed the size limit.", emailDto.Recipient);
        }
        catch (AttachmentUnavailableException ex)
        {
            // Drop the whole email,  a referenced file could not be retrieved.
            _logger.LogError(ex, "Email to {Recipient} not sent: an attachment could not be retrieved.", emailDto.Recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending via SMTP to {Recipient}", emailDto.Recipient);
        }
        finally
        {
            foreach (var att in attachments)
                att.Dispose();
        }
    }
}
