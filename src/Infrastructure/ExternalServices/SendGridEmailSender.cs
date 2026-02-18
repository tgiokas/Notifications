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
    private readonly ITemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(ITemplateService templateService, IConfiguration config, ILogger<SendGridEmailSender> logger)
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

            _logger.LogInformation("Constructing SendGrid email to {Recipient}...", emailDto.Recipient);

            string htmlBody = emailDto.TemplateParams is null && !string.IsNullOrEmpty(emailDto.Message)
                ? emailDto.Message
                : await _templateService.RenderAsync(
                    emailDto.Type ?? EmailTemplateType.Generic,
                    emailDto.TemplateParams ?? new Dictionary<string, string>());           

            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(emailDto.Recipient);
            var msg = MailHelper.CreateSingleEmail(from, to, emailDto.Subject, plainTextContent: null, htmlContent: htmlBody);

            // Reply-to
            if (emailDto.ReplyTo is { Count: > 0 })
            {
                msg.SetReplyTo(new EmailAddress(emailDto.ReplyTo[0]));
                
                if (emailDto.ReplyTo.Count > 1)
                {
                    msg.AddReplyTos(emailDto.ReplyTo
                        .Select(r => new EmailAddress(r))
                        .ToList());
                }
            }

            var client = new SendGridClient(apiKey);

            var response = await client.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SendGrid email sent to {Recipient}. Status: {StatusCode}",
                    emailDto.Recipient, response.StatusCode);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync(cancellationToken);
                _logger.LogError("SendGrid API error for {Recipient}. Status: {StatusCode}, Body: {Body}",
                    emailDto.Recipient, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending via SendGrid to {Recipient}", emailDto.Recipient);
        }
    }
}
