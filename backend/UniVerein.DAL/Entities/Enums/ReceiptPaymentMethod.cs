using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum ReceiptPaymentMethod
{
    [Display(Name = "CASH")]
    CASH,
    [Display(Name = "BANK_TRANSFER")]
    BANK_TRANSFER,
    [Display(Name = "CARD")]
    CARD,
    [Display(Name = "SEPA_DIRECT_DEBIT")]
    SEPA_DIRECT_DEBIT,
    [Display(Name = "PAYPAL")]
    PAYPAL,
    [Display(Name = "OTHER")]
    OTHER
}
