using System.Collections.Generic;

namespace UniVerein.Api.Models.Sepa;

public class SepaDirectDebitDocument
{
    public GroupHeader GroupHeader { get; set; } = new();
    public List<PaymentInfo> PaymentInfos { get; set; } = new();
}