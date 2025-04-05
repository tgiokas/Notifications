namespace Notifications.WebApi.Middlewares;

public class TokenMiddleware
{
    private readonly RequestDelegate _next;

    public TokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var token = TokenHelper.ExtractAccessToken(context);
        if (!string.IsNullOrEmpty(token))
        {
            context.Items["AccessToken"] = token;
        }

        await _next(context);
    }

    public static class TokenHelper
    {
        public static string ExtractAccessToken(HttpContext httpContext)
        {
            var authorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                return null;
            }

            return authorizationHeader.Split(" ").Last();
        }
    }
}
