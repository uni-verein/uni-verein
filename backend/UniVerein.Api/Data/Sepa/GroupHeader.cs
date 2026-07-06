using System;

namespace UniVerein.Api.Data.Sepa;

public class GroupHeader
{
    public string MessageId { get; set; } = string.Empty;
    public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;
    public int NumberOfTxs { get; set; }
    public decimal ControlSum { get; set; }
    public Party InitiatingParty { get; set; } = new();
}