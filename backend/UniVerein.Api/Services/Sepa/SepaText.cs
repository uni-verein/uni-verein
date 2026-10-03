using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UniVerein.Api.Services.Sepa;

public static class SepaText
{
    // EPC Latin character set (EPC130-08 SDD Core C2PSP IG, chapter 1.4). National extensions such as
    // German umlauts are only allowed by bilateral agreement and are therefore converted.
    private const string AllowedTextSpecialChars = "/-?:().,'+ ";

    // Identifiers (MsgId, PmtInfId, InstrId, EndToEndId, MndtId) must not contain spaces.
    private const string AllowedIdSpecialChars = "/-?:().,'+";

    private static readonly Dictionary<char, string> Transliterations = new()
    {
        ['Ä'] = "Ae",
        ['Ö'] = "Oe",
        ['Ü'] = "Ue",
        ['ä'] = "ae",
        ['ö'] = "oe",
        ['ü'] = "ue",
        ['ß'] = "ss",
        ['&'] = "+",
        ['Æ'] = "AE",
        ['æ'] = "ae",
        ['Œ'] = "OE",
        ['œ'] = "oe",
        ['Ø'] = "O",
        ['ø'] = "o",
        ['Ł'] = "L",
        ['ł'] = "l",
        ['Đ'] = "D",
        ['đ'] = "d",
        ['Þ'] = "TH",
        ['þ'] = "th",
        ['_'] = "-",
        ['–'] = "-",
        ['—'] = "-",
        ['‘'] = "'",
        ['’'] = "'",
        ['´'] = "'",
        ['`'] = "'",
        ['"'] = "'",
        ['“'] = "'",
        ['”'] = "'",
        ['„'] = "'"
    };

    private static readonly Regex MultipleSpaces = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex CountryCodePattern = new("^[A-Z]{2}$", RegexOptions.Compiled);
    // ISO 9362:2014 (BICFIDec2014Identifier): the institution code may contain digits.
    private static readonly Regex BicPattern = new("^[A-Z0-9]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled);

    public static string Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string text = Convert(value, AllowedTextSpecialChars, replacement: ' ');
        text = MultipleSpaces.Replace(text, " ").Trim();
        return Cut(text, maxLength).TrimEnd();
    }

    public static string Id(string? value, int maxLength = 35)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string id = Convert(value.Trim(), AllowedIdSpecialChars, replacement: '-');
        while (id.Contains("//"))
            id = id.Replace("//", "/");

        return Cut(id.Trim('/'), maxLength).TrimEnd('/');
    }

    public static string? CountryCode(string? value)
    {
        string code = (value ?? string.Empty).Trim().ToUpperInvariant();
        return CountryCodePattern.IsMatch(code) ? code : null;
    }

    public static string? Bic(string? value)
    {
        string bic = (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return BicPattern.IsMatch(bic) ? bic : null;
    }

    // The creditor identifier is case and space insensitive (EPC130-08, chapter 1.5.2).
    public static string CreditorId(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
    }

    public static string Iban(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static string Convert(string value, string allowedSpecialChars, char replacement)
    {
        StringBuilder sb = new(value.Length);
        foreach (char c in NormalizeComposed(value))
        {
            if (char.IsAsciiLetterOrDigit(c) || allowedSpecialChars.Contains(c))
                sb.Append(c);
            else if (Transliterations.TryGetValue(c, out string? transliteration))
                sb.Append(transliteration);
            else
                sb.Append(RemoveDiacritics(c) ?? replacement.ToString());
        }

        return sb.ToString();
    }

    // Combines decomposed umlauts (u + ¨ -> ü), strings with broken surrogates are processed as they are.
    private static string NormalizeComposed(string value)
    {
        try
        {
            return value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return value;
        }
    }

    // é -> e, ñ -> n, ... null if the character has no Latin base letter.
    private static string? RemoveDiacritics(char c)
    {
        // Halves of surrogate pairs (e.g. emojis) cannot be normalized on their own.
        if (char.IsSurrogate(c))
            return null;

        string baseChars = new(c.ToString()
            .Normalize(NormalizationForm.FormD)
            .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return baseChars.Length > 0 && baseChars.All(char.IsAsciiLetterOrDigit) ? baseChars : null;
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
