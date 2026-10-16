using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum UserSettingType
{
    [Display(Name = "RECEIPT_NOTIFICATION")]
    RECEIPT_NOTIFICATION,
    [Display(Name = "SELF_ENROLLMENT_NOTIFICATION")]
    SELF_ENROLLMENT_NOTIFICATION,
    [Display(Name = "CALENDAR_INVITE_EMAIL")]
    CALENDAR_INVITE_EMAIL,
    [Display(Name = "CALENDAR_CHANGE_EMAIL")]
    CALENDAR_CHANGE_EMAIL,
    [Display(Name = "CALENDAR_REMINDER_EMAIL")]
    CALENDAR_REMINDER_EMAIL
}
