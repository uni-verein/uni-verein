using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum UserSettingType
{
    [Display(Name = "RECEIPT_NOTIFICATION")]
    RECEIPT_NOTIFICATION,

    [Display(Name = "SELF_ENROLLMENT_NOTIFICATION")]
    SELF_ENROLLMENT_NOTIFICATION
}
