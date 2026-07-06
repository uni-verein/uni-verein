using System.Collections.Generic;

namespace UniVerein.Api.Data.Sepa;

public class SepaDirectDebitDocument
{
    public GroupHeader GroupHeader { get; set; } = new();
    public List<PaymentInfo> PaymentInfos { get; set; } = new();
}