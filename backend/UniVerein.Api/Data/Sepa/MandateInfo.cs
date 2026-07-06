using System;

namespace UniVerein.Api.Data.Sepa;

public class MandateInfo
{
    public string MandateId { get; set; } = string.Empty;
    public DateOnly DateOfSignature { get; set; }
    public bool AmendmentIndicator { get; set; } = false;
}