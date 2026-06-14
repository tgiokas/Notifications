using Microsoft.Extensions.Configuration;

namespace Notifications.Application.Configuration;

public class AttachmentSettings
{
    public string StorageBaseUrl { get; set; } = string.Empty;

    /// Maximum combined size of all attachments on a single email.
    /// Default 25 MB — safe under Gmail/SMTP (~25 MB) and SendGrid (~30 MB) limits.
    public long MaxTotalBytes { get; set; } = 25L * 1024 * 1024;

    public static AttachmentSettings BindFromConfiguration(IConfiguration configuration) => new()
    {
        StorageBaseUrl = configuration["STORAGE_BASE_URL"]
            ?? throw new ArgumentNullException(nameof(configuration), "STORAGE_BASE_URL is not set."),

        MaxTotalBytes = long.TryParse(configuration["ATTACHMENT_MAX_TOTAL_BYTES"], out var v) && v > 0
            ? v
            : 25L * 1024 * 1024
    };
}