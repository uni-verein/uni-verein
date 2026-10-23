using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum MembershipStatus
{
    [Display(Name = "MEMBER")]
    MEMBER,
    [Display(Name = "SUPPORTING_MEMBER")]
    SUPPORTING_MEMBER
}
