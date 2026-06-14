namespace Notifications.Application.Interfaces;

/// Client over StorageService's GET /Documents/download endpoint.
public interface IStorageApiClient
{
    /// Downloads a single object from StorageService.
    /// Throws AttachmentUnavailableException if the object cannot be retrieved
    /// (not found, decryption failure, transport error, or any non-200 response).
    Task<ResolvedAttachment> DownloadAsync(string bucket, string key, CancellationToken cancellationToken = default);
}

/// A downloaded attachment ready to be attached to an email.
/// Owns the underlying stream — dispose when done.
public sealed class ResolvedAttachment : IDisposable
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }

    public void Dispose() => Content.Dispose();
}
