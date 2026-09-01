using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SendGrid;
using SendGrid.Helpers.Mail;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class SendGridEmailSender : IEmailSender
{
    private readonly ITemplateService _templateService;
    private readonly IAttachmentResolver _attachmentResolver;
    private readonly SendGridSettings _sendGrid;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        ITemplateService templateService,
        IAttachmentResolver attachmentResolver,
        IOptions<EmailSettings> emailSettings,
        ILogger<SendGridEmailSender> logger)
    {
        _templateService = templateService;
        _attachmentResolver = attachmentResolver;
        _sendGrid = emailSettings.Value.SendGrid;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ResolvedAttachment> attachments = Array.Empty<ResolvedAttachment>();

        try
        {
            // Resolve attachments first. If any is too large or unavailable, this throws and
            // the whole email is dropped (caught below) before we call the SendGrid API.
            attachments = await _attachmentResolver.ResolveAsync(emailDto.Attachments, cancellationToken);

            var fromEmail = !string.IsNullOrWhiteSpace(emailDto.Sender)
                ? emailDto.Sender
                : _sendGrid.FromEmail;

            _logger.LogInformation("Constructing SendGrid email...");

            // Render HTML body
            string htmlBody = emailDto.Type is null
                ? emailDto.Message ?? string.Empty
                : await _templateService.RenderAsync((EmailTemplateType)emailDto.Type, emailDto.TemplateParams ?? new Dictionary<string, string>());

            var message = new SendGridMessage();
            message.SetFrom(new EmailAddress(fromEmail, _sendGrid.FromName));
            message.SetSubject(emailDto.Subject);
            message.AddContent(MimeType.Html, htmlBody);

            var toAddresses = emailDto.GetAllToRecipients().ToList();
            if (!emailDto.HasAnyRecipients())
                throw new ArgumentException("At least one recipient (To, Cc, or Bcc) is required.");

            message.AddTos(toAddresses.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.Cc is { Count: > 0 })
                message.AddCcs(emailDto.Cc.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.Bcc is { Count: > 0 })
                message.AddBccs(emailDto.Bcc.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.ReplyTo is { Count: 1 })
                message.ReplyTo = new EmailAddress(emailDto.ReplyTo[0]);
            else if (emailDto.ReplyTo is { Count: > 1 })
                message.ReplyTos = emailDto.ReplyTo.Select(r => new EmailAddress(r)).ToList();

            // SendGrid attachments must be base64-encoded.
            foreach (var att in attachments)
            {
                string base64;
                if (att.Content is MemoryStream mem && mem.TryGetBuffer(out var seg))
                {
                    base64 = Convert.ToBase64String(seg.Array!, seg.Offset, seg.Count);
                }
                else
                {
                    // Fallback for any other stream type.
                    using var ms = new MemoryStream();
                    await att.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                    base64 = Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
                }

                message.AddAttachment(att.FileName, base64, att.ContentType);
            }

            var client = new SendGridClient(_sendGrid.ApiKey);
            var response = await client.SendEmailAsync(message, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "SendGrid email sent. To: [{ToRecipients}] Cc: [{CcRecipients}] Bcc: [{BccRecipients}]",
                     string.Join(", ", toAddresses),
                     string.Join(", ", emailDto.GetAllCcRecipients()),
                     string.Join(", ", emailDto.GetAllBccRecipients()));
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("SendGrid API error for {Recipients}. Status: {StatusCode}, Body: {Body}",
                    string.Join(", ", toAddresses), response.StatusCode, body);
            }
        }
        catch (AttachmentTooLargeException ex)
        {
            // Drop the whole email and Log
            _logger.LogError(ex, "Email to {Recipient} not sent: attachments exceed the size limit.", emailDto.Recipient);
        }
        catch (AttachmentUnavailableException ex) when (ex.IsTransient)
        {
            throw new TransientDeliveryException("An attachment was temporarily unavailable.", ex);
        }
        catch (AttachmentUnavailableException ex)
        {
            // Drop the whole email and Log
            _logger.LogError(ex, "Email to {Recipient} not sent: an attachment could not be retrieved.", emailDto.Recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending via SendGrid to {Recipient}", emailDto.Recipient);
        }
        finally
        {
            foreach (var att in attachments)
                att.Dispose();
        }
    }
}
