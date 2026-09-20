using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models;

public class PendingSelfEnrollmentApprovalResult
{
    public PendingSelfEnrollmentActionStatus Status { get; }
    public MemberEntity? Member { get; }

    public PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus status, MemberEntity? member = null)
    {
        Status = status;
        Member = member;
    }
}
