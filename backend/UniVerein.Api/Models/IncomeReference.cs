using System;

namespace UniVerein.Api.Models;

public class IncomeReference
{
    public Guid Id { get; set; }
    public bool IsManual { get; set; }
    public bool IsPaid { get; set; }
    public DateTime SortDate { get; set; }
}
