using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.ExternalServices;

public sealed class AttachmentResolver : IAttachmentResolver
{
    private readonly IStorageApiClient _storage;
    private readonly AttachmentSettings _settings;
    private readonly ILogger<AttachmentResolver> _logger;

    public AttachmentResolver(
        IStorageApiClient storage,
        IOptions<AttachmentSettings> settings,
        ILogger<AttachmentResolver> logger)
    {
        _storage = storage;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResolvedAttachment>> ResolveAsync(
        IReadOnlyList<EmailAttachmentDto>? attachments, CancellationToken ct = default)
    {
        if (attachments is null || attachments.Count == 0)
            return Array.Empty<ResolvedAttachment>();

        var resolved = new List<ResolvedAttachment>(attachments.Count);
        long total = 0;

        try
        {
            foreach (var a in attachments)
            {
                if (string.IsNullOrWhiteSpace(a.Bucket) || string.IsNullOrWhiteSpace(a.Key))
                {
                    // A malformed reference can't be honoured. Per policy, the whole email is dropped.
                    throw new AttachmentUnavailableException(
                        a.Bucket ?? "(null)", a.Key ?? "(null)", 0, "Attachment reference is missing bucket or key.");
                }

                // Throws AttachmentUnavailableException on any non-200 from StorageService.
                var file = await _storage.DownloadAsync(a.Bucket, a.Key, ct).ConfigureAwait(false);

                var item = new ResolvedAttachment
                {
                    Content = file.Content,
                    FileName = a.FileName ?? file.FileName,
                    ContentType = a.ContentType ?? file.ContentType,
                    Size = file.Size
                };

                total += item.Size;
                if (total > _settings.MaxTotalBytes)
                {
                    item.Dispose();
                    throw new AttachmentTooLargeException(total, _settings.MaxTotalBytes);
                }

                resolved.Add(item);
            }

            return resolved;
        }
        catch
        {
            // Clean up anything already downloaded before the failure propagates.
            foreach (var r in resolved)
                r.Dispose();

            throw;
        }
    }
}
