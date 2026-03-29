namespace Notifications.Application.Dtos;

public class EmailTemplateRequestDto
{    
    /// Template type key (e.g. "VerificationLink", "MfaCode", or a custom string).    
    public string TemplateType { get; set; } = string.Empty;

    ///Human-readable name for UI display.
    public string Name { get; set; } = string.Empty;

    ///Optional description.
    public string? Description { get; set; }
   
    /// The HTML content, Base64-encoded.
    public string HtmlContentBase64 { get; set; } = string.Empty;

    ///Default subject line (may contain {{Token}} placeholders).
    public string? DefaultSubject { get; set; }

    /// Comma-separated token names the template expects (e.g. "Username,VerificationLink").
    /// Used by the UI to render token input fields. 
    public string? TokenDefinitions { get; set; }
}
