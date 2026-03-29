using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SendGrid;
using SendGrid.Helpers.Mail;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class SendGridEmailSender : IEmailSender
{
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        IEmailTemplateService templateService,
        IConfiguration config,
        ILogger<SendGridEmailSender> logger)
    {
        _templateService = templateService;
        _configuration = config;
        _logger = logger;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = _configuration["SENDGRID_API_KEY"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SENDGRID_API_KEY is not set.");
            var fromEmail = !string.IsNullOrWhiteSpace(emailDto.Sender)
                ? emailDto.Sender
                : _configuration["SENDGRID_FROM_EMAIL"]
                    ?? throw new ArgumentNullException(nameof(_configuration), "SENDGRID_FROM_EMAIL is not set.");
            var fromName = _configuration["SENDGRID_FROM_NAME"] ?? "Notifications";

            _logger.LogInformation("Constructing SendGrid email...");

            // Render HTML body
            string htmlBody = emailDto.TemplateParams is null && !string.IsNullOrEmpty(emailDto.Message)
                ? emailDto.Message
                : await _templateService.RenderAsync(
                    emailDto.Type ?? EmailTemplateType.Generic,
                    emailDto.TemplateParams ?? new Dictionary<string, string>());
                       
            var message = new SendGridMessage();
            var from = new EmailAddress(fromEmail, fromName);
            message.SetFrom(from);
            message.SetSubject(emailDto.Subject);
            message.AddContent(MimeType.Html, htmlBody);

            // Collect all To recipients
            var toAddresses = emailDto.GetAllToRecipients().ToList();
            if (toAddresses.Count == 0)
                throw new ArgumentException("At least one recipient is required.");

            // To
            message.AddTos(toAddresses.Select(r => new EmailAddress(r)).ToList());

            // CC
            if (emailDto.Cc is { Count: > 0 })
            {
                message.AddCcs(emailDto.Cc.Select(r => new EmailAddress(r)).ToList());
            }

            // BCC
            if (emailDto.Bcc is { Count: > 0 })
            {
                message.AddBccs(emailDto.Bcc.Select(r => new EmailAddress(r)).ToList());
            }

            // Reply-to
            if (emailDto.ReplyTo is { Count: 1 })
            {
                message.ReplyTo = new EmailAddress(emailDto.ReplyTo[0]);
            }
            else if (emailDto.ReplyTo is { Count: > 1 })
            {
                message.ReplyTos = emailDto.ReplyTo
                    .Select(r => new EmailAddress(r))
                    .ToList();
            }

            var client = new SendGridClient(apiKey);

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