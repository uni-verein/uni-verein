using System;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace UniVerein.Api.Services;

public static class RequestLogLevel
{
    private const string HealthPath = "/health";

    public static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMs, Exception? exception)
    {
        if (exception != null || httpContext.Response.StatusCode >= 500)
            return LogEventLevel.Error;

        return httpContext.Request.Path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase)
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    }
}
