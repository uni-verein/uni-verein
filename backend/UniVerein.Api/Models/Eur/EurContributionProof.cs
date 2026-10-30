using System;

namespace UniVerein.Api.Models.Eur;

public class EurContributionProof
{
    public string Number { get; }
    public DateTime PaymentDate { get; }
    public DateTime DueDate { get; }
    public int MemberNumber { get; }
    public decimal Amount { get; }

    public EurContributionProof(string number, DateTime paymentDate, DateTime dueDate, int memberNumber, decimal amount)
    {
        Number = number;
        PaymentDate = paymentDate;
        DueDate = dueDate;
        MemberNumber = memberNumber;
        Amount = amount;
    }
}
