using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniVerein.Api.Helper;

public static partial class EmbedOrigins
{
    [GeneratedRegex(@"^https?://(?:\*\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+|[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*)(?::(\d{1,5}))?\z")]
    private static partial Regex OriginPattern();

    public static List<string> Parse(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ParseValid(string? raw)
    {
        return Parse(raw).Where(IsValid).ToList();
    }

    public static bool IsValid(string origin)
    {
        Match match = OriginPattern().Match(origin);
        if (!match.Success)
            return false;

        Group port = match.Groups[1];
        return !port.Success || int.Parse(port.Value) is >= 1 and <= 65535;
    }
}
