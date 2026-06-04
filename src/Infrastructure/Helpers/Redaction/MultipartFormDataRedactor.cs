namespace Notifications.Infrastructure.Helpers.Redaction;

public static class MultipartFormDataRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "file"       
    };

    public static string TryRedact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Find the boundary from the first line
        var firstLineEnd = input.IndexOf('\n');
        if (firstLineEnd < 0) return input;
        var boundary = input.Substring(0, firstLineEnd).Trim();

        var parts = input.Split(boundary, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrWhiteSpace(part) || !part.Contains("Content-Disposition"))
                continue;

            foreach (var key in SensitiveKeys)
            {
                if (part.Contains($"name=\"{key}\"", StringComparison.OrdinalIgnoreCase))
                {
                    // Find header/content separator
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
                }
            }
        }

        return string.Join(boundary, parts);
    }
}
