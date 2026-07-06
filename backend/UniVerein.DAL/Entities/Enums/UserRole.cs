using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum UserRole
{
    [Display(Name = "ADMIN")] 
    ADMIN,
    [Display(Name = "USER")] 
    USER,
    [Display(Name = "FINANCIAL_MANAGER")] 
    FINANCIAL_MANAGER
}