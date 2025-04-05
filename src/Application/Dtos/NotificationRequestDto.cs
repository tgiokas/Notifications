namespace Notifications.Application.DTOs;

public class NotificationRequestDto
{
    public string Recipient { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Channel { get; set; } = "Email"; // Enum as string for API input simplicity
}
