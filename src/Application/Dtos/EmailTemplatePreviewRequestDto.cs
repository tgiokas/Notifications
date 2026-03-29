namespace Notifications.Application.Dtos;

public class EmailTemplatePreviewRequestDto
{
    /// Token values to substitute into the template (e.g. { "Username": "John" }).
    public Dictionary<string, string> Tokens { get; set; } = new();
}
