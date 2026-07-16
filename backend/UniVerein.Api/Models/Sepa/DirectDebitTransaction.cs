namespace UniVerein.Api.Models.Sepa;

public class DirectDebitTransaction
{
    public string InstructionId { get; set; } = string.Empty;
    public string EndToEndId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public MandateInfo Mandate { get; set; } = new();
    public Party Debtor { get; set; } = new();
    public BankAccount DebtorAccount { get; set; } = new();
    public FinancialInstitution DebtorAgent { get; set; } = new();
    public string? PurposeCode { get; set; }
    public string RemittanceInfo { get; set; } = string.Empty;
}