using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class MemberRequest
{
    [Required]
    [JsonPropertyName("gender")]
    public Gender Gender { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("firstName")]
    public required string FirstName { get; set; }

    [JsonPropertyName("middleName")] 
    public string MiddleName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("lastName")]
    public required string LastName { get; set; }

    [Required]
    [JsonPropertyName("birthday")]
    public DateTimeOffset Birthday { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("street")]
    public required string Street { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("postalCode")]
    public required string PostalCode { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("city")]
    public required string City { get; set; }
    
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("countryCode")]
    public required string CountryCode { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("phone")] 
    public string Phone { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("bulkMail")]
    public BulkMail BulkMail { get; set; }

    [Required]
    [JsonPropertyName("startOfStudies")]
    public DateTimeOffset StartOfStudies { get; set; }

    [JsonPropertyName("endOfStudies")] 
    public DateTimeOffset? EndOfStudies { get; set; }

    [JsonPropertyName("academicDegree")] 
    public AcademicDegree? AcademicDegree { get; set; }

    [JsonPropertyName("courseOfStudy")] 
    public string CourseOfStudy { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("taskWithinTheClub")]
    public TaskWithinTheClub TaskWithinTheClub { get; set; }

    [Required]
    [JsonPropertyName("memberCategoryId")]
    public Guid MemberCategoryId { get; set; }

    [JsonPropertyName("iban")] 
    public string IBAN { get; set; } = string.Empty;

    [JsonPropertyName("bic")] 
    public string Bic { get; set; } = string.Empty;

    [JsonPropertyName("sepaConsent")] 
    public DateTimeOffset? SepaConsent { get; set; }

    [Required]
    [JsonPropertyName("entryDate")]
    public DateTimeOffset EntryDate { get; set; }

    [JsonPropertyName("exitDate")] 
    public DateTimeOffset? ExitDate { get; set; }

    [JsonPropertyName("contributionPlanId")]
    public Guid? ContributionPlanId { get; set; }
}