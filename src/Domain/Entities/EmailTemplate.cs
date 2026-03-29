using Notifications.Domain.Enums;

namespace Notifications.Domain.Entities;

public class EmailTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
   
    /// Maps to <see cref="EmailTemplateType"/> but stored as string for flexibility
    /// when custom template types are added without redeploying.   
    public string TemplateType { get; set; } = string.Empty;

    /// Human-readable name shown in UI (e.g. "Verification Link Email").
    public string Name { get; set; } = string.Empty;

    /// Optional description for UI editors.
    public string? Description { get; set; }

    /// The raw HTML content of the template.
    public string HtmlContent { get; set; } = string.Empty;

    /// Default subject line (can contain {{tokens}}).
    public string? DefaultSubject { get; set; }
    
    /// Comma-separated list of expected token names (e.g. "Username,VerificationLink").
    /// Used by the UI to show token editors.   
    public string? TokenDefinitions { get; set; }

    /// Only the active template for a given type is used for sending.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
