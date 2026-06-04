using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

using Notifications.Infrastructure.Helpers.Redaction;

namespace Notifications.Infrastructure.ApiClients;

public abstract class ApiClientBase
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    const string ErrorMessageTemplate =
        "ERROR {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload}";

    protected ApiClientBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var requestContentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;

        string requestBody;
        if (requestContentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            requestBody = $"[{requestContentType}]";
        }
        else if (requestContentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var requestBodyRaw = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            requestBody = FormUrlEncodedRedactor.TryRedact(requestBodyRaw);
        }
        else if (IsTextLike(requestContentType))
        {
            requestBody = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
        }
        else
        {
            requestBody = $"[non-text {requestContentType}]";
        }

        var sw = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ErrorMessageTemplate, "Outgoing", request.Method,
                request.RequestUri, requestBody, HttpStatusCode.ServiceUnavailable, "");

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Service is temporarily unavailable.")
            };
        }

        sw.Stop();

        var responseContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        string responseBody;
        if (IsTextLike(responseContentType))
        {
            string responseBodyRaw = await response.Content.ReadAsStringAsync(cancellationToken);
            responseBody = JsonRedactor.TryRedact(responseBodyRaw);
        }
        else
        {
            long? len = response.Content.Headers.ContentLength;
            responseBody = len.HasValue
                ? $"[non-text {responseContentType}, {len.Value} bytes]"
                : $"[non-text {responseContentType}]";
        }

        int statusCode = (int)response.StatusCode;
        LogLevel logLevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

        _logger.Log(logLevel, LogMessageTemplate, "Outgoing", request.Method,
            request.RequestUri, requestBody, statusCode, responseBody, (long)sw.ElapsedMilliseconds);

        return response;
    }

    private static bool IsTextLike(string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType)) return false;
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) return true;
        if (mediaType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
