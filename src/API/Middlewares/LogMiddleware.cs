using System.Diagnostics;

namespace Notifications.Api.Middlewares;
public class LogMiddleware
{
    private readonly RequestDelegate _next;

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    public LogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext, ILogger<LogMiddleware> logger)
    {
        string requestBody = await GetRequestBody(httpContext.Request);
        
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
            int statusCode = httpContext.Response.StatusCode;
            LogLevel loglevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

            // Log using Serilog
            logger.Log(loglevel, LogMessageTemplate, "Incoming", httpContext.Request.Method,
                httpContext.Request.Path, requestBody, statusCode, responseBody, (long)sw.Elapsed.TotalMilliseconds);

            httpContext.Response.Body = originalBodyStream;
            
            if (!httpContext.Response.HasStarted)
            {
                newMemoryStream.Seek(0, SeekOrigin.Begin);
                await newMemoryStream.CopyToAsync(originalBodyStream);
            }
        }
    }

    private static async Task<string> GetRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        string bodyAsText = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        return $"{request.QueryString} {bodyAsText}";
    }

    private static async Task<string> GetResponseBody(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        string responseString = await new StreamReader(response.Body).ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);

        return responseString;
    }
}

