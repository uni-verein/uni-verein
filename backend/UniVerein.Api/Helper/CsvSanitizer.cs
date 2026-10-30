using System.Text.RegularExpressions;

namespace UniVerein.Api.Helper;

public static class CsvSanitizer
{
    private const char EscapePrefix = '\'';
    private static readonly Regex PhoneNumberRegex = new(@"^\+[0-9][0-9 ()./-]*$", RegexOptions.Compiled);

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ? EscapePrefix + value : value;
    }

    public static string? SanitizePhoneNumber(string? value)
    {
        if (value != null && PhoneNumberRegex.IsMatch(value))
            return value;

        return Sanitize(value);
    }
}
