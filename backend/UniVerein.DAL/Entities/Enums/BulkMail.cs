using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum BulkMail
{
    [Display(Name = "ALLOWED")] 
    ALLOWED,
    [Display(Name = "NOT_ALLOWED")] 
    NOT_ALLOWED
}