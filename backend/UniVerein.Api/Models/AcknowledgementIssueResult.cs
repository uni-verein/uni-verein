using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models;

public class AcknowledgementIssueResult
{
    public AcknowledgementOutcome Outcome { get; init; }
    public PaymentAcknowledgementEntity? Acknowledgement { get; init; }
}
