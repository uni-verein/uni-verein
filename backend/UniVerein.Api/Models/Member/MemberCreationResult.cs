using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models;

public class MemberCreationResult
{
    public MemberCreationStatus Status { get; private init; }
    public MemberEntity? Member { get; private init; }

    public static MemberCreationResult Success(MemberEntity member)
    {
        return new MemberCreationResult
        {
            Status = MemberCreationStatus.SUCCESS,
            Member = member
        };
    }

    public static MemberCreationResult Failure(MemberCreationStatus status)
    {
        return new MemberCreationResult
        {
            Status = status
        };
    }
}