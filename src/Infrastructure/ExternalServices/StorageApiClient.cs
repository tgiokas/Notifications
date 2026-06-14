using System.Net;

using Microsoft.Extensions.Logging;

using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;

using Notifications.Infrastructure.ApiClients;

namespace Notifications.Infrastructure.ExternalServices;

/// Talks to StorageService's download endpoint via the shared ApiClientBase
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
