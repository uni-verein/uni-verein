using System;

namespace UniVerein.Api.Services.LoginNotification;

public static class UserAgentParser
{
    public static DeviceDescription Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return new DeviceDescription(null, null);

        return new DeviceDescription(ParseBrowser(userAgent), ParseOperatingSystem(userAgent));
    }

    private static string? ParseBrowser(string ua)
    {
        if (Contains(ua, "Edg/") || Contains(ua, "EdgA/") || Contains(ua, "EdgiOS/"))
            return "Edge";
        if (Contains(ua, "OPR/") || Contains(ua, "Opera"))
            return "Opera";
        if (Contains(ua, "SamsungBrowser/"))
            return "Samsung Internet";
        if (Contains(ua, "Firefox/") || Contains(ua, "FxiOS/"))
            return "Firefox";
        if (Contains(ua, "Chrome/") || Contains(ua, "CriOS/"))
            return "Chrome";
        if (Contains(ua, "Safari/") && Contains(ua, "Version/"))
            return "Safari";
        return null;
    }

    private static string? ParseOperatingSystem(string ua)
    {
        if (Contains(ua, "Windows"))
            return "Windows";
        if (Contains(ua, "Android"))
            return "Android";
        if (Contains(ua, "iPhone") || Contains(ua, "iPad") || Contains(ua, "iPod"))
            return "iOS";
        if (Contains(ua, "Macintosh") || Contains(ua, "Mac OS X"))
            return "macOS";
        if (Contains(ua, "CrOS"))
            return "ChromeOS";
        if (Contains(ua, "Linux") || Contains(ua, "X11"))
            return "Linux";
        return null;
    }

    private static bool Contains(string value, string part)
    {
        return value.Contains(part, StringComparison.Ordinal);
    }
}
