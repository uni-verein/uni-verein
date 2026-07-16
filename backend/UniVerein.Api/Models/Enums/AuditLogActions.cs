using System.ComponentModel.DataAnnotations;

namespace UniVerein.Api.Models.Enums;

public enum AuditLogActions
{
    [Display(Name = "RESTORE")] RESTORE,
    [Display(Name = "CREATE")] CREATE,
    [Display(Name = "UPDATE")] UPDATE,
    [Display(Name = "DELETE")] DELETE,
    [Display(Name = "SOFT_DELETE")] SOFT_DELETE
}