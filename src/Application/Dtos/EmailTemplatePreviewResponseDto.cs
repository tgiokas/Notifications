namespace Notifications.Application.Dtos;

public class EmailTemplatePreviewResponseDto
{
    /// Rendered HTML as Base64
    public string RenderedHtmlBase64 { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;   
}
