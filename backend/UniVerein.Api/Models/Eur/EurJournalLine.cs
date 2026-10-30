using System;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.Eur;

public class EurJournalLine
{
    public string Number { get; }
    public DateTime Date { get; }
    public string Text { get; }
    public string Category { get; }
    public TaxArea Area { get; }
    public bool IsIncome { get; }
    public string? Account { get; }
    public decimal Amount { get; }

    public EurJournalLine(string number, DateTime date, string text, string category, TaxArea area, bool isIncome, string? account, decimal amount)
    {
        Number = number;
        Date = date;
        Text = text;
        Category = category;
        Area = area;
        IsIncome = isIncome;
        Account = account;
        Amount = amount;
    }

    public string Check => Amount == 0 ? "Betrag fehlt" : string.IsNullOrEmpty(Number) ? "Beleg-Nr. fehlt" : "OK";
}
