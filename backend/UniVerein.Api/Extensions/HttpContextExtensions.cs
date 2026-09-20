using System.Linq;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIpAddress(this HttpContext context)
    {
        string? xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xRealIp))
            return xRealIp;

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
