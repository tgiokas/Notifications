using Notifications.Domain.Enums;

namespace Notifications.Application.Dtos;

public class NotificationEmailDto
{
    public string Recipient { get; set; } = string.Empty;
    public string? Sender { get; set; }
    public List<string>? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public EmailTemplateType? Type { get; set; }
    public Dictionary<string, string>? TemplateParams { get; set; }
}