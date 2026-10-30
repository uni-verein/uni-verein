using System;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.Eur;

public static class EurLabels
{
    public const string Membership = "Mitgliedsbeiträge";
    public const string OtherIncome = "Sonstige Einnahmen";
    public const string OtherExpense = "Sonstige Ausgaben";
    public const string Bank = "Bank";
    public const string Cash = "Kasse";

    public static string AreaName(TaxArea area) => area switch
    {
        TaxArea.NON_PROFIT => "1 Ideeller Bereich",
        TaxArea.ASSET_MANAGEMENT => "2 Vermögensverwaltung",
        TaxArea.PURPOSE_RELATED_OPERATION => "3 Zweckbetrieb",
        TaxArea.COMMERCIAL_OPERATION => "4 Wirtschaftlicher Geschäftsbetrieb",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null)
    };

    public static string Kind(bool isIncome) => isIncome ? "Einnahme" : "Ausgabe";
}
