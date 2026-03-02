using Notifications.Domain.Enums;

namespace Notifications.Application.Dtos;

public class NotificationEmailDto
{
    /// Primary recipient (kept for backward compatibility) 
    public string Recipient { get; set; } = string.Empty;
    /// Optional Additional To recipients.
    public List<string>? Recipients { get; set; }
    /// Optional CC recipients.
    public List<string>? Cc { get; set; }
    /// Optional BCC recipients.
    public List<string>? Bcc { get; set; }
    /// Optional sender override. 
    public string? Sender { get; set; }
    public List<string>? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public EmailTemplateType? Type { get; set; }
    public Dictionary<string, string>? TemplateParams { get; set; }
 
    /// Returns all To addresses: the primary Recipient plus any additional Recipients.
    public List<string> GetAllToRecipients()
    {
        var all = new List<string>();

        if (!string.IsNullOrWhiteSpace(Recipient))
            all.Add(Recipient);

        if (Recipients is not null)
            all.AddRange(Recipients.Where(r => !string.IsNullOrWhiteSpace(r)));

        return all;
    }
}