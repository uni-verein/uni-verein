using System;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Security;

public static class AuthCookie
{
    public const string Name = "uv_auth";
    public const string CsrfHeader = "X-UV-CSRF";

    public static void Append(HttpContext context, string token, DateTimeOffset expires)
    {
        context.Response.Cookies.Append(Name, token, CreateOptions(context.Request, expires));
    }

    public static void Delete(HttpContext context)
    {
        context.Response.Cookies.Delete(Name, CreateOptions(context.Request, null));
    }

    public static bool TryRead(HttpRequest request, out string token)
    {
        token = request.Cookies[Name] ?? string.Empty;
        return token.Length > 0;
    }

    public static bool IsSafeMethod(HttpRequest request)
    {
        return HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
               HttpMethods.IsOptions(request.Method);
    }

    public static bool HasCsrfHeader(HttpRequest request)
    {
        return request.Headers.ContainsKey(CsrfHeader);
    }

    private static CookieOptions CreateOptions(HttpRequest request, DateTimeOffset? expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            // TLS ends at the reverse proxy, so the request itself is plain http there.
            Secure = request.IsHttps ||
                     string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase),
            Path = "/",
            Expires = expires
        };
    }
}
