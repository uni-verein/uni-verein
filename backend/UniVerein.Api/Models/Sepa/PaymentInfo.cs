using System;
using System.Collections.Generic;
using UniVerein.Api.Models.Enums;

namespace UniVerein.Api.Models.Sepa;

public class PaymentInfo
{
    public string PaymentInfoId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "DD";
    public bool BatchBooking { get; set; } = false;
    public int NumberOfTxs { get; set; }
    public decimal ControlSum { get; set; }
    public LocalInstrumentCode LocalInstrument { get; set; } = LocalInstrumentCode.CORE;
    public SequenceType SequenceType { get; set; } = SequenceType.RCUR;
    public DateOnly RequestedCollectionDate { get; set; }
    public Party Creditor { get; set; } = new();
    public BankAccount CreditorAccount { get; set; } = new();
    public FinancialInstitution CreditorAgent { get; set; } = new();

    // Creditor Identifier, e.g. DE98ZZZ09999999999
    public string CreditorSchemeId { get; set; } = string.Empty;
    public string ChargeBearer { get; set; } = "SLEV";
    public List<DirectDebitTransaction> Transactions { get; set; } = new();
}