using System.Diagnostics;

using Notifications.Infrastructure.Helpers.Redaction;

namespace Notifications.Api.Middlewares;

public class LogMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxPayloadLength = 4096;

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    public LogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext, ILogger<LogMiddleware> logger)
    {
        var (query, body) = await GetRequestParts(httpContext.Request);

        // Redact request body
        string safeRequestBody = body;
        if (IsJson(httpContext.Request))
        {
            safeRequestBody = JsonRedactor.TryRedact(body);
        }
        else if (IsFormData(httpContext.Request))
        {
            safeRequestBody = MultipartFormDataRedactor.TryRedact(body);
        }
        safeRequestBody = Truncate(safeRequestBody, MaxPayloadLength);


        // Copy a pointer to the original response body stream
        Stream originalBodyStream = httpContext.Response.Body;

        // Create a new memory stream and use it for the temporary response body
        using var newMemoryStream = new MemoryStream();
        httpContext.Response.Body = newMemoryStream;

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(httpContext);
        }
        finally
        {
            sw.Stop();

            string responseBody = await GetResponseBody(httpContext.Response);

            // Redact response body (may contain tokens)
            string safeResponseBody = JsonRedactor.TryRedact(responseBody);
            safeResponseBody = Truncate(safeResponseBody, MaxPayloadLength);

            int statusCode = httpContext.Response.StatusCode;
            LogLevel loglevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

            // Log using Serilog          
            logger.Log(loglevel, LogMessageTemplate, "Incoming", httpContext.Request.Method,
              httpContext.Request.Path, safeRequestBody, statusCode, safeResponseBody, (long)sw.Elapsed.TotalMilliseconds);

            httpContext.Response.Body = originalBodyStream;

            if (!httpContext.Response.HasStarted)
            {
                newMemoryStream.Seek(0, SeekOrigin.Begin);
                await newMemoryStream.CopyToAsync(originalBodyStream);
            }
        }
    }

    private static async Task<(string query, string body)> GetRequestParts(HttpRequest request)
    {
        request.EnableBuffering();
        string body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return (request.QueryString.ToString(), body);
    }

    private static async Task<string> GetResponseBody(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        string responseString = await new StreamReader(response.Body).ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);

        return responseString;
    }

    private static string Truncate(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLen) return input;
        return input.Substring(0, maxLen) + "…(truncated)";
    }

    private static bool IsJson(HttpRequest req)
    {
        return req.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsFormData(HttpRequest req)
    {
        return req.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;
    }
}