using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class MemberUpdateRequest
{
    [JsonPropertyName("gender")] 
    public Gender? Gender { get; set; }

    [JsonPropertyName("firstName")] 
    public string? FirstName { get; set; }

    [JsonPropertyName("middleName")] 
    public string? MiddleName { get; set; }

    [JsonPropertyName("lastName")] 
    public string? LastName { get; set; }

    [JsonPropertyName("birthday")] 
    public DateTimeOffset? Birthday { get; set; }

    [JsonPropertyName("street")] 
    public string? Street { get; set; }

    [JsonPropertyName("postalCode")] 
    public string? PostalCode { get; set; }

    [JsonPropertyName("city")] 
    public string? City { get; set; }
    
    [JsonPropertyName("countryCode")] 
    public string? CountryCode { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")] 
    public string? Phone { get; set; }

    [JsonPropertyName("bulkMail")] 
    public BulkMail? BulkMail { get; set; }

    [JsonPropertyName("startOfStudies")]
    public DateTimeOffset? StartOfStudies { get; set; }

    [JsonPropertyName("endOfStudies")] 
    public DateTimeOffset? EndOfStudies { get; set; }

    [JsonPropertyName("academicDegree")] 
    public AcademicDegree? AcademicDegree { get; set; }

    [JsonPropertyName("courseOfStudy")]
    public string? CourseOfStudy { get; set; }

    [JsonPropertyName("taskWithinTheClub")]
    public TaskWithinTheClub? TaskWithinTheClub { get; set; }

    [JsonPropertyName("memberCategoryId")] 
    public Guid? MemberCategoryId { get; set; }

    [JsonPropertyName("iban")] 
    public string? IBAN { get; set; }

    [JsonPropertyName("bic")] 
    public string? Bic { get; set; }

    [JsonPropertyName("sepaConsent")]
    public DateTimeOffset? SepaConsent { get; set; }

    [JsonPropertyName("entryDate")]
    public DateTimeOffset? EntryDate { get; set; }

    [JsonPropertyName("exitDate")] 
    public DateTimeOffset? ExitDate { get; set; }

    [JsonPropertyName("contributionPlanId")]
    public Guid? ContributionPlanId { get; set; }
}