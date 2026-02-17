using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.ExternalServices;

public class EmailSender : IEmailSender
{
    private readonly ITemplateService _templateService; 
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ITemplateService templateService, IConfiguration config, ILogger<EmailSender> logger)
    {
        _templateService = templateService;
        _logger = logger;
        _configuration = config;
    }

    public async Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        try
        {            
            var host = _configuration["SMTP_HOST"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SMTP_HOST is not set.");
            var portStr = _configuration["SMTP_PORT"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SMTP_PORT is not set.");
            if (!int.TryParse(portStr, out var port))
                throw new ArgumentException("SMTP_PORT is not a valid integer.", nameof(_configuration));
            var username = _configuration["SMTP_USERNAME"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SMTP_USERNAME is not set.");
            var password = _configuration["SMTP_PASSWORD"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SMTP_PASSWORD is not set.");
            var from = _configuration["SMTP_FROM"]
                ?? throw new ArgumentNullException(nameof(_configuration), "SMTP_FROM is not set.");

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),               
                UseDefaultCredentials = true                
            };

            _logger.LogInformation("Constructing Email...");

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

            message.To.Add(new MailAddress(emailDto.Recipient));
            
            if (emailDto.ReplyTo != null)
            {
                foreach (var replyToAddress in emailDto.ReplyTo)
                {
                    message.ReplyToList.Add(new MailAddress(replyToAddress));
                }
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent to {Recipient}", emailDto.Recipient);
        }
        catch (SmtpFailedRecipientException ex)
        {
            _logger.LogError(ex, "Failed recipient {Recipient}. Response: {StatusCode}", emailDto.Recipient, ex.StatusCode);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending to {Recipient}: {Message}", emailDto.Recipient, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending to {Recipient}", emailDto.Recipient);
        }
    }
}
