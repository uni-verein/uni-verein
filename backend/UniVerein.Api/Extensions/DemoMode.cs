using System;
using Microsoft.Extensions.Configuration;

namespace UniVerein.Api.Extensions;

public static class DemoMode
{
    // Set Demo__Enabled=true in the environment of the public demo deployment.
    public const string ConfigKey = "Demo:Enabled";

    public static bool IsResetEnabled(IConfiguration configuration)
    {
        string? value = configuration[ConfigKey]?.Trim();

        return value != null &&
               (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("1", StringComparison.Ordinal) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsEnabled(IConfiguration configuration)
    {
        string? value = configuration[ConfigKey]?.Trim();

        return !string.IsNullOrEmpty(value) &&
               !value.Equals("false", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("0", StringComparison.Ordinal) &&
               !value.Equals("no", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("off", StringComparison.OrdinalIgnoreCase);
    }
}
