using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("Members")]
public class MemberEntity : BaseEntity
{
    [Column("mandate_id")] 
    public required string MandateId { get; set; } = string.Empty;

    [Column("member_number")] 
    public int MemberNumber { get; set; }

    [Column("gender")] 
    public Gender Gender { get; set; }

    [Column("first_name")] 
    public required string FirstName { get; set; } = string.Empty;

    [Column("middle_name")] 
    public string MiddleName { get; set; } = string.Empty;

    [Column("last_name")] 
    public required string LastName { get; set; } = string.Empty;

    [Column("birthday")] 
    public required string BirthdayEncrypted { get; set; } = string.Empty;

    [Column("street")] 
    public required string StreetEncrypted { get; set; } = string.Empty;

    [Column("postal_code")] 
    public required string PostalCode { get; set; } = string.Empty;

    [Column("city")] 
    public required string City { get; set; } = string.Empty;

    [Column("country_code")] 
    public string? CountryCode { get; set; }

    [Column("emailEncrypted")] 
    public string EmailEncrypted { get; set; } = string.Empty;

    [Column("emailHash")] 
    public string EmailHash { get; set; } = string.Empty;

    [Column("phone")] 
    public string PhoneEncrypted { get; set; } = string.Empty;

    [Column("bulk_mail")] 
    public BulkMail BulkMail { get; set; } = BulkMail.ALLOWED;

    [Column("start_of_studies")] 
    public DateTimeOffset StartOfStudies { get; set; }

    [Column("end_of_studies")] 
    public DateTimeOffset? EndOfStudies { get; set; }

    [Column("academic_degree")] 
    public AcademicDegree? AcademicDegree { get; set; }

    [Column("course_of_study")] 
    public string CourseOfStudy { get; set; } = string.Empty;

    [Column("task_within_the_club")] 
    public TaskWithinTheClub TaskWithinTheClub { get; set; }

    [Column("iban_encrypted")] 
    public string? IBAN_Encrypted { get; set; }

    [Column("iban_hash")] 
    public string? IBAN_Hash { get; set; }

    [Column("bic_encrypted")] 
    public string? Bic_Encrypted { get; set; }

    [Column("sepa_consent")] 
    public DateTimeOffset? SepaConsent { get; set; }

    [Column("entry_date")] 
    public DateTimeOffset EntryDate { get; set; }

    [Column("exit_date")] 
    public DateTimeOffset? ExitDate { get; set; }

    [Column("member_category_id")] 
    public Guid? MemberCategoryId { get; set; }

    [ForeignKey("MemberCategoryId")] 
    public MemberCategoryEntity? MemberCategory { get; set; }

    [Column("contribution_plan_id")] 
    public Guid? ContributionPlanId { get; set; }

    [ForeignKey("ContributionPlanId")] 
    public ContributionPlanEntity? ContributionPlan { get; set; }
}