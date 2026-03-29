namespace Notifications.Application.Dtos;

public class EmailTemplateResponseDto
{
    public Guid Id { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// HTML content as a Base64-encoded string.
    public string HtmlContentBase64 { get; set; } = string.Empty;
    public string? DefaultSubject { get; set; }
    public string? TokenDefinitions { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
