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

    /// True when the failure looks temporary (server-side or timeout) and a retry
    /// may succeed. Permanent failures — 404, 403, or the malformed-reference case
    /// (status 0) — stay false so they're dropped rather than retried.
    public bool IsTransient => StatusCode is 408 or 429 or 500 or 502 or 503 or 504;
}
