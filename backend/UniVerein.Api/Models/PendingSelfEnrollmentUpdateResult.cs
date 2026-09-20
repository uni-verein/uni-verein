using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models;

public class PendingSelfEnrollmentUpdateResult
{
    public PendingSelfEnrollmentActionStatus Status { get; }
    public PendingSelfEnrollmentEntity? Pending { get; }

    public PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus status,
        PendingSelfEnrollmentEntity? pending = null)
    {
        Status = status;
        Pending = pending;
    }
}
