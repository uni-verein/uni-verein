namespace UniVerein.Api.Models.Sepa;

public class BankAccount
{
    public string IBAN { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
}