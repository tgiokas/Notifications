namespace Notifications.Application.Dtos;

public class NotificationDto
{
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string>? Tokens { get; set; }
}