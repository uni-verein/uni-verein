using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace UniVerein.Api.Services;

// Keeps secrets that are part of a request path (the personal calendar feed token) out of the request log.
public static partial class RequestLogSanitizer
{
    public const string RedactedSegment = "<redacted>";

    [GeneratedRegex(@"^(/calendar/feed/)[^/]+(\.ics/?)$", RegexOptions.IgnoreCase)]
    private static partial Regex FeedPathRegex();

    public static string Redact(string path)
    {
        return FeedPathRegex().Replace(path, $"$1{RedactedSegment}$2");
    }

    // Same properties as the default request log message of Serilog, with a redacted path.
    public static IEnumerable<LogEventProperty> GetMessageTemplateProperties(HttpContext httpContext, string requestPath,
        double elapsedMs, int statusCode)
    {
        return
        [
            new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty("RequestPath", new ScalarValue(Redact(requestPath))),
            new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
            new LogEventProperty("Elapsed", new ScalarValue(elapsedMs))
        ];
    }
}
