using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum Interval
{
    [Display(Name = "MONTHLY")] 
    MONTHLY,
    [Display(Name = "YEARLY")]
    YEARLY
}