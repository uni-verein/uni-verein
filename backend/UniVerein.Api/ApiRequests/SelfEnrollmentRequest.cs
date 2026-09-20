using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class SelfEnrollmentRequest
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
    [EmailAddress]
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

    // What the applicant does professionally/academically and why they want to join - replaces letting
    // the applicant pick their own MemberCategoryId (removed below). A reviewer reads this and assigns
    // the category themselves before approving (see PendingEnrollmentForm.tsx).
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("motivation")]
    public required string Motivation { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("iban")]
    public required string IBAN { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("bic")]
    public required string Bic { get; set; }

    [JsonPropertyName("sepaConsent")]
    public DateTimeOffset? SepaConsent { get; set; }

    [Required]
    [JsonPropertyName("entryDate")]
    public DateTimeOffset EntryDate { get; set; }

    // MemberCategoryId/ContributionPlanId are deliberately not part of this request at all (not just
    // optional) - it doesn't make sense for a self-enrolling applicant to assign their own category or
    // contribution tier. A reviewer assigns both via PendingEnrollmentForm.tsx before approving.
}
