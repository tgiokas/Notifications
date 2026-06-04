namespace Notifications.Application.Exceptions;

/// Thrown when a referenced attachment cannot be retrieved from StorageService
/// (object not found, decryption failure, or any non-200 response).
/// Causes the whole email to be dropped.
public class AttachmentUnavailableException : Exception
{
    public AttachmentUnavailableException(string bucket, string key, int statusCode, string body)
        : base($"Attachment {bucket}/{key} is not accessible (HTTP {statusCode}): {body}")
    {
        Bucket = bucket;
        Key = key;
        StatusCode = statusCode;
    }

    public string Bucket { get; }
    public string Key { get; }
    public int StatusCode { get; }
}
