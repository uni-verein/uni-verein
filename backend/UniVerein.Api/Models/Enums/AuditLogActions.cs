using System.ComponentModel.DataAnnotations;

namespace UniVerein.Api.Models.Enums;

public enum AuditLogActions
{
    [Display(Name = "RESTORE")] RESTORE,
    [Display(Name = "CREATE")] CREATE,
    [Display(Name = "UPDATE")] UPDATE,
    [Display(Name = "DELETE")] DELETE,
    [Display(Name = "SOFT_DELETE")] SOFT_DELETE,
    [Display(Name = "IMPORT")] IMPORT,
    [Display(Name = "TWO_FACTOR_ENABLE")] TWO_FACTOR_ENABLE,
    [Display(Name = "TWO_FACTOR_DISABLE")] TWO_FACTOR_DISABLE,
    [Display(Name = "TWO_FACTOR_RESET")] TWO_FACTOR_RESET
}