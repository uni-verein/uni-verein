using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class MemberResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("memberNumber")] 
    public int MemberNumber { get; set; }

    [JsonPropertyName("gender")] 
    public Gender Gender { get; set; }

    [JsonPropertyName("firstName")] 
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("middleName")] 
    public string MiddleName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")] 
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("birthday")] 
    public DateTimeOffset Birthday { get; set; }

    [JsonPropertyName("street")] 
    public string Street { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")] 
    public string PostalCode { get; set; } = string.Empty;

    [JsonPropertyName("city")] 
    public string City { get; set; } = string.Empty;
    
    [JsonPropertyName("countryCode")] 
    public string CountryCode { get; set; } = string.Empty;
    
    [JsonPropertyName("email")] 
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")] 
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("bulkMail")] 
    public BulkMail BulkMail { get; set; }

    [JsonPropertyName("startOfStudies")] 
    public DateTimeOffset StartOfStudies { get; set; }

    [JsonPropertyName("endOfStudies")] 
    public DateTimeOffset? EndOfStudies { get; set; }

    [JsonPropertyName("academicDegree")] 
    public AcademicDegree? AcademicDegree { get; set; }

    [JsonPropertyName("courseOfStudy")] 
    public string CourseOfStudy { get; set; } = string.Empty;

    [JsonPropertyName("taskWithinTheClub")]
    public TaskWithinTheClub TaskWithinTheClub { get; set; }

    [JsonPropertyName("memberCategoryId")] 
    public Guid? MemberCategoryId { get; set; }

    [JsonPropertyName("iban")]
    public string IBAN { get; set; } = string.Empty;

    [JsonPropertyName("bic")]
    public string Bic { get; set; } = string.Empty;

    [JsonPropertyName("sepaConsent")] 
    public DateTimeOffset? SepaConsent { get; set; }

    [JsonPropertyName("entryDate")] 
    public DateTimeOffset EntryDate { get; set; }

    [JsonPropertyName("exitDate")] 
    public DateTimeOffset? ExitDate { get; set; }

    [JsonPropertyName("contributionPlanId")]
    public Guid? ContributionPlanId { get; set; }

    [JsonPropertyName("deletedAt")] 
    public DateTimeOffset? DeletedAt { get; set; }
}