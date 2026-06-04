namespace Notifications.Application.Dtos;

/// <summary>
/// Reference to a file stored in StorageService that should be attached to an email.
/// The file is pulled at send time via StorageService's GET /documents/download endpoint.
/// References only — file bytes never travel through Kafka.
/// </summary>
public class EmailAttachmentDto
{
    /// <summary>StorageService bucket the object lives in.</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>StorageService object key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Optional display name override. Falls back to the name StorageService returns.</summary>
    public string? FileName { get; set; }

    /// <summary>Optional content-type override. Falls back to the type StorageService returns.</summary>
    public string? ContentType { get; set; }
}
