using System;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.Eur;

internal class EurPendingLine
{
    public string Number { get; }
    public DateTime Date { get; }
    public int Order { get; }
    public string Text { get; }
    public string Category { get; }
    public TaxArea Area { get; }
    public bool IsIncome { get; }
    public string? Account { get; }
    public decimal Amount { get; }

    public EurPendingLine(string number, DateTime date, int order, string text, string category, TaxArea area, bool isIncome, string? account, decimal amount)
    {
        Number = number;
        Date = date;
        Order = order;
        Text = text;
        Category = category;
        Area = area;
        IsIncome = isIncome;
        Account = account;
        Amount = amount;
    }
}
