namespace Notifications.Application.Exceptions;

/// Thrown when the combined size of an email's attachments exceeds the configured limit.
/// Causes the whole email to be dropped.
public sealed class AttachmentTooLargeException : Exception
{
    public AttachmentTooLargeException(long totalBytes, long maxBytes)
        : base($"Total attachment size {totalBytes} bytes exceeds the limit of {maxBytes} bytes.")
    {
        TotalBytes = totalBytes;
        MaxBytes = maxBytes;
    }

    public long TotalBytes { get; }
    public long MaxBytes { get; }
}
