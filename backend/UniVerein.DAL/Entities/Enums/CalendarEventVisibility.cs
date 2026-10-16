using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum CalendarEventVisibility
{
    [Display(Name = "CLUB")]
    CLUB,
    [Display(Name = "PARTICIPANTS")]
    PARTICIPANTS,
    [Display(Name = "PRIVATE")]
    PRIVATE
}
