using System.Net;

using Microsoft.Extensions.Logging;

using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;

// ApiClientBase is reused as-is. Adjust this using to wherever you place ApiClientBase
// (and its Redaction helpers) inside the Notifications solution / shared library.
using Notifications.Infrastructure.ApiClients;

namespace Notifications.Infrastructure.ExternalServices;

/// <summary>
/// Talks to StorageService's download endpoint via the shared ApiClientBase, so it inherits
/// the standard outgoing-request logging, payload redaction and transport-failure handling.
///
/// StorageService returns 200 + bytes on success and 202 (Accepted) + a JSON error envelope
/// on failure. 202 is a 2xx, so we cannot rely on IsSuccessStatusCode — we require exactly 200
/// and treat everything else (including the base class's 503 transport fallback) as unavailable.
/// </summary>
public class StorageApiClient : ApiClientBase, IStorageApiClient
{
    private const string DownloadEndpoint = "/Documents/download";

    public StorageApiClient(HttpClient httpClient, ILogger<StorageApiClient> logger)
        : base(httpClient, logger)
    {
    }

    public async Task<ResolvedAttachment> DownloadAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        var url = $"{DownloadEndpoint}?bucket={Uri.EscapeDataString(bucket)}&key={Uri.EscapeDataString(key)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendRequestAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AttachmentUnavailableException(bucket, key, (int)response.StatusCode, body);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? key.Split('/').Last();

        // ApiClientBase uses ResponseContentRead, so the body is already buffered; reading it
        // again as bytes is safe and returns the original (non-stringified) content.
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return new ResolvedAttachment
        {
            Content = new MemoryStream(bytes),
            FileName = fileName,
            ContentType = contentType,
            Size = bytes.LongLength
        };
    }
}
