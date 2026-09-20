using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models;

public class PendingSelfEnrollmentSubmitResult
{
    public MemberCreationStatus Status { get; private init; }
    public PendingSelfEnrollmentEntity? PendingSelfEnrollment { get; private init; }

    public static PendingSelfEnrollmentSubmitResult Success(PendingSelfEnrollmentEntity entity)
    {
        return new PendingSelfEnrollmentSubmitResult
        {
            Status = MemberCreationStatus.SUCCESS,
            PendingSelfEnrollment = entity
        };
    }

    public static PendingSelfEnrollmentSubmitResult Failure(MemberCreationStatus status)
    {
        return new PendingSelfEnrollmentSubmitResult { Status = status };
    }
}
