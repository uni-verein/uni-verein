using System;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models;

public class MemberCreationInput
{
    public Gender Gender { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset Birthday { get; set; }
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public BulkMail BulkMail { get; set; }
    public DateTimeOffset StartOfStudies { get; set; }
    public DateTimeOffset? EndOfStudies { get; set; }
    public AcademicDegree? AcademicDegree { get; set; }
    public string CourseOfStudy { get; set; } = string.Empty;
    public TaskWithinTheClub TaskWithinTheClub { get; set; }
    public Guid MemberCategoryId { get; set; }
    public string IBAN { get; set; } = string.Empty;
    public string Bic { get; set; } = string.Empty;
    public DateTimeOffset? SepaConsent { get; set; }
    public DateTimeOffset EntryDate { get; set; }
    public DateTimeOffset? ExitDate { get; set; }
    public Guid? ContributionPlanId { get; set; }
}