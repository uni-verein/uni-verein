using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum Gender
{
    [Display(Name = "MALE")] 
    MALE,
    [Display(Name = "FEMALE")] 
    FEMALE,
    [Display(Name = "DIVERSE")] 
    DIVERSE
}