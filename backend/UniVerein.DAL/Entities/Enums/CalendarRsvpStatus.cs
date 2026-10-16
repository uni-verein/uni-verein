using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum CalendarRsvpStatus
{
    [Display(Name = "PENDING")]
    PENDING,
    [Display(Name = "ACCEPTED")]
    ACCEPTED,
    [Display(Name = "TENTATIVE")]
    TENTATIVE,
    [Display(Name = "DECLINED")]
    DECLINED
}
