using System.Collections.Generic;

namespace UniVerein.Api.Services.Sepa;

public static class SepaCountries
{
    private static readonly HashSet<string> NonEeaSepaCountries =
    [
        "AD", "AL", "CH", "GB", "MC",
        "MD", "ME", "MK", "RS", "SM",
        "VA", "GG", "JE", "IM", "PM"
    ];

    public static bool IsNonEeaPsp(string? iban, string? bic)
    {
        string? normalizedBic = SepaText.Bic(bic);
        string country = normalizedBic != null
            ? normalizedBic.Substring(4, 2)
            : SepaText.Iban(iban) is { Length: >= 2 } normalizedIban ? normalizedIban[..2] : string.Empty;

        return NonEeaSepaCountries.Contains(country);
    }
}
