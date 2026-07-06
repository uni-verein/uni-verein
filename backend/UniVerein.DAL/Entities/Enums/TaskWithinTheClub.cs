using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum TaskWithinTheClub
{
    [Display(Name = "MEMBER")] 
    MEMBER,
    [Display(Name = "CHAIRMAN")] 
    CHAIRMAN,
    [Display(Name = "SECOND_CHAIRMAN")] 
    SECOND_CHAIRMAN,
    [Display(Name = "JUNIOR_BOARD_MEMBER")]
    JUNIOR_BOARD_MEMBER,
    [Display(Name = "CHIEF_FINANCE_OFFICER")]
    CHIEF_FINANCE_OFFICER,
    [Display(Name = "WEBSITE_MANAGER")] 
    WEBSITE_MANAGER,
    [Display(Name = "ALUMNI_OFFICER")] 
    ALUMNI_OFFICER,
    [Display(Name = "STUDENT_COUNCIL_REPRESENTATIVE")]
    STUDENT_COUNCIL_REPRESENTATIVE
}