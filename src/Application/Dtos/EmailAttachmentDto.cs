namespace Notifications.Application.Dtos;

/// Reference to a file stored in StorageService that should be attached to an email.
/// The file is pulled at send time via StorageService GET /documents/download endpoint.
public class EmailAttachmentDto
{
    public string Bucket { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }
}