using System;
using System.Text.RegularExpressions;
using UniVerein.Api.Models;
using UniVerein.Api.Services.Sepa;

namespace UniVerein.Api.Validators;

public static class SepaValidator
{
    public static void ValidateCreditorConfig(CreditorConfig creditorConfig)
    {
        if (string.IsNullOrWhiteSpace(creditorConfig.Name))
            throw new ArgumentException("Creditor.Name must not be empty.");

        if (!IsValidIban(creditorConfig.Iban))
            throw new ArgumentException($"Creditor.Iban '{creditorConfig.Iban}' is invalid.");

        if (!IsValidBic(creditorConfig.Bic))
            throw new ArgumentException($"Creditor.Bic '{creditorConfig.Bic}' is invalid.");

        if (string.IsNullOrWhiteSpace(creditorConfig.CreditorId))
            throw new ArgumentException("Creditor.CreditorId (CreditorId-ID) must not be empty.");

        if (!IsValidCreditorId(creditorConfig.CreditorId))
            throw new ArgumentException($"Creditor.CreditorId '{creditorConfig.CreditorId}' is invalid.");

        if (string.IsNullOrWhiteSpace(creditorConfig.TownName))
            throw new ArgumentException("Creditor.TownName must not be empty (Mandatory from November 2026).");

        if (SepaText.CountryCode(creditorConfig.Country) == null)
            throw new ArgumentException("Creditor.Country must be a 2-digit ISO country code.");
    }

    public static bool IsValidIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        string normalized = SepaText.Iban(iban);
        return Regex.IsMatch(normalized, @"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$") && HasValidChecksum(normalized);
    }

    public static bool IsValidCreditorId(string? creditorId)
    {
        string id = SepaText.CreditorId(creditorId);
        if (!Regex.IsMatch(id, @"^[A-Z]{2}\d{2}[A-Z0-9]{3}[A-Z0-9+?/:().,'-]{1,28}$"))
            return false;

        string national = Regex.Replace(id[7..], "[^A-Z0-9]", string.Empty);
        if (national.Length == 0)
            return false;

        int remainder = Mod97(national + id[..2] + "00");
        return int.Parse(id[2..4]) == 98 - remainder;
    }

    // ISO 13616 check digits: move the first four characters to the end, convert letters to numbers
    // (A=10 ... Z=35) and the remainder modulo 97 must be 1.
    private static bool HasValidChecksum(string iban)
    {
        return Mod97(iban[4..] + iban[..4]) == 1;
    }

    // Letters are converted to numbers (A=10 ... Z=35) before calculating the remainder modulo 97.
    private static int Mod97(string value)
    {
        int remainder = 0;
        foreach (char c in value)
        {
            int digit = char.IsDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = ((digit < 10 ? remainder * 10 : remainder * 100) + digit) % 97;
        }

        return remainder;
    }

    private static bool IsValidBic(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return false;

        return SepaText.Bic(bic) != null;
    }
}