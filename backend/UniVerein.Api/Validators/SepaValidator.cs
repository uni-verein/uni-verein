using System;
using System.Text.RegularExpressions;
using UniVerein.Api.Data;

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

        if (string.IsNullOrWhiteSpace(creditorConfig.TownName))
            throw new ArgumentException("Creditor.TownName must not be empty (Mandatory from November 2026).");

        if (string.IsNullOrWhiteSpace(creditorConfig.Country) || creditorConfig.Country.Length != 2)
            throw new ArgumentException("Creditor.Country must be a 2-digit ISO country code.");
    }

    private static bool IsValidIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        return Regex.IsMatch(iban.Replace(" ", "").ToUpperInvariant(), @"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$");
    }

    private static bool IsValidBic(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return false;

        return Regex.IsMatch(bic.Trim(), @"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$");
    }
}