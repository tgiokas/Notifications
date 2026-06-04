using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

/// Resolves a list of attachment references into downloaded, in-memory attachments,
/// enforcing the configured total-size cap.
public interface IAttachmentResolver
{
    /// Downloads every referenced attachment from StorageService.
    /// Throws AttachmentTooLargeException if the running total exceeds the limit,
    /// or AttachmentUnavailableException if any reference cannot be retrieved.
    /// On any failure, already-downloaded items are disposed before the exception propagates.
    Task<IReadOnlyList<ResolvedAttachment>> ResolveAsync(
        IReadOnlyList<EmailAttachmentDto>? attachments, CancellationToken ct = default);
}
