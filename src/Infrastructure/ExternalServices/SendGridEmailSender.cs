using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SendGrid;
using SendGrid.Helpers.Mail;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class SendGridEmailSender : IEmailSender
{
    private readonly ITemplateService _templateService;
    private readonly SendGridSettings _sendGrid;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        ITemplateService templateService,
        IOptions<EmailSettings> emailSettings,
        ILogger<SendGridEmailSender> logger)
    {
        _templateService = templateService;
        _sendGrid = emailSettings.Value.SendGrid;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var fromEmail = !string.IsNullOrWhiteSpace(emailDto.Sender)
                ? emailDto.Sender
                : _sendGrid.FromEmail;

            _logger.LogInformation("Constructing SendGrid email...");

            string htmlBody = emailDto.TemplateParams is null && !string.IsNullOrEmpty(emailDto.Message)
                ? emailDto.Message
                : await _templateService.RenderAsync(
                    emailDto.Type ?? EmailTemplateType.Generic,
                    emailDto.TemplateParams ?? new Dictionary<string, string>());

            var message = new SendGridMessage();
            message.SetFrom(new EmailAddress(fromEmail, _sendGrid.FromName));
            message.SetSubject(emailDto.Subject);
            message.AddContent(MimeType.Html, htmlBody);

            var toAddresses = emailDto.GetAllToRecipients().ToList();
            if (toAddresses.Count == 0)
                throw new ArgumentException("At least one recipient is required.");

            message.AddTos(toAddresses.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.Cc is { Count: > 0 })
                message.AddCcs(emailDto.Cc.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.Bcc is { Count: > 0 })
                message.AddBccs(emailDto.Bcc.Select(r => new EmailAddress(r)).ToList());

            if (emailDto.ReplyTo is { Count: 1 })
                message.ReplyTo = new EmailAddress(emailDto.ReplyTo[0]);
            else if (emailDto.ReplyTo is { Count: > 1 })
                message.ReplyTos = emailDto.ReplyTo.Select(r => new EmailAddress(r)).ToList();

            var client = new SendGridClient(_sendGrid.ApiKey);
            var response = await client.SendEmailAsync(message, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SendGrid email sent to {Recipients}. Status: {StatusCode}",
                    string.Join(", ", toAddresses), response.StatusCode);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("SendGrid API error for {Recipients}. Status: {StatusCode}, Body: {Body}",
                    string.Join(", ", toAddresses), response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending via SendGrid to {Recipient}", emailDto.Recipient);
        }
    }
}