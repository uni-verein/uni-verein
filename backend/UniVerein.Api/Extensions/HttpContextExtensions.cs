using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIpAddress(this HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static Guid GetActorId(this HttpContext context)
    {
        return context.User.GetActorId();
    }

    public static Guid GetActorId(this ClaimsPrincipal user)
    {
        string? value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out Guid id) ? id : Guid.Empty;
    }
}
