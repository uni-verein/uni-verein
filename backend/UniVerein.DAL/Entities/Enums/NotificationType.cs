using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum NotificationType
{
    [Display(Name = "CALENDAR_INVITATION")]
    CALENDAR_INVITATION,
    [Display(Name = "CALENDAR_EVENT_CHANGED")]
    CALENDAR_EVENT_CHANGED,
    [Display(Name = "CALENDAR_EVENT_DELETED")]
    CALENDAR_EVENT_DELETED,
    [Display(Name = "CALENDAR_RSVP")]
    CALENDAR_RSVP,
    [Display(Name = "CALENDAR_REMINDER")]
    CALENDAR_REMINDER
}
