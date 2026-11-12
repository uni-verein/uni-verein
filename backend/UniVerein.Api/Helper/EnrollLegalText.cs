using System.Collections.Concurrent;

namespace UniVerein.Api.Helper;

public static class EnrollLegalText
{
    private const int MaxCachedEntries = 32;

    private static readonly ConcurrentDictionary<string, string> Cache = new();

    public static string Render(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        if (Cache.TryGetValue(stored, out string? cached))
            return cached;

        string sanitized = MailHtmlSanitizer.Sanitize(stored);
        if (!MailHtmlSanitizer.HasVisibleContent(sanitized))
            sanitized = string.Empty;

        if (Cache.Count >= MaxCachedEntries)
            Cache.Clear();
        Cache[stored] = sanitized;

        return sanitized;
    }
}
