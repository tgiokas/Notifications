namespace Notifications.Infrastructure.Helpers.Redaction;

public static class MultipartFormDataRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "file",
        "password",
        "newPassword",
        "pass",
        "token",
        "idToken",
        "accessToken",
        "refreshToken",
        "loginToken",
        "setupToken",
        "clientSecret",
        "code"
    };

    /// <summary>
    /// Redacts the body of any multipart part whose <c>name="..."</c> is in the sensitive list.
    /// The boundary must come from the request's <c>Content-Type</c> header (the <c>boundary</c>
    /// parameter value, without the leading <c>--</c>). If the boundary is missing or empty the
    /// input is returned unchanged.
    /// </summary>
    public static string TryRedact(string input, string? boundary)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(boundary))
            return input;

        // RFC 7578: parts are separated by the boundary prefixed with "--".
        var delimiter = "--" + boundary.Trim().Trim('"');

        var parts = input.Split(delimiter, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrWhiteSpace(part) || !part.Contains("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var key in SensitiveKeys)
            {
                if (part.Contains($"name=\"{key}\"", StringComparison.OrdinalIgnoreCase))
                {
                    var headerEnd = part.IndexOf("\r\n\r\n");
                    var sepLength = 4;
                    if (headerEnd < 0)
                    {
                        headerEnd = part.IndexOf("\n\n");
                        sepLength = 2;
                    }
                    if (headerEnd >= 0)
                    {
                        var headers = part.Substring(0, headerEnd + sepLength);
                        parts[i] = headers + RedactedValue + "\r\n";
                    }
                    break;
                }
            }
        }

        return string.Join(delimiter, parts);
    }
}
