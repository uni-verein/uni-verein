using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class PendingSelfEnrollmentUpdateRequest
{
    [JsonPropertyName("gender")]
    public Gender? Gender { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("birthday")]
    public DateTimeOffset? Birthday { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [MaxLength(10)]
    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("city")]
    public string? City { get; set; }

    [MaxLength(3)]
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    [MaxLength(254)]
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [MaxLength(50)]
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

    [MaxLength(100)]
    [JsonPropertyName("courseOfStudy")]
    public string? CourseOfStudy { get; set; }

    [MaxLength(1000)]
    [JsonPropertyName("motivation")]
    public string? Motivation { get; set; }

    [JsonPropertyName("memberCategoryId")]
    public Guid? MemberCategoryId { get; set; }

    [MaxLength(50)]
    [JsonPropertyName("iban")]
    public string? IBAN { get; set; }

    [MaxLength(20)]
    [JsonPropertyName("bic")]
    public string? Bic { get; set; }

    [JsonPropertyName("contributionPlanId")]
    public Guid? ContributionPlanId { get; set; }
}
