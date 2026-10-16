using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum CalendarParticipantRole
{
    [Display(Name = "EDITOR")]
    EDITOR,
    [Display(Name = "ATTENDEE")]
    ATTENDEE
}
