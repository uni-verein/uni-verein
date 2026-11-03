using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Security;

public static partial class DeviceCookie
{
    public const string Name = "uv_device";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(400);

    public static string CreateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public static void Append(HttpContext context, string token, DateTimeOffset now)
    {
        context.Response.Cookies.Append(Name, token, AuthCookie.CreateOptions(context.Request, now + Lifetime));
    }

    public static string? TryRead(HttpRequest request)
    {
        string? value = request.Cookies[Name];
        return value != null && TokenFormat().IsMatch(value) ? value : null;
    }

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")]
    private static partial Regex TokenFormat();
}
