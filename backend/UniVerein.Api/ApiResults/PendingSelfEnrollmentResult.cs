using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class PendingSelfEnrollmentResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("gender")]
    public Gender Gender { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("middleName")]
    public string MiddleName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("memberCategoryId")]
    public Guid? MemberCategoryId { get; set; }

    [JsonPropertyName("memberCategoryName")]
    public string? MemberCategoryName { get; set; }

    [JsonPropertyName("contributionPlanId")]
    public Guid? ContributionPlanId { get; set; }

    [JsonPropertyName("submittedAt")]
    public DateTimeOffset SubmittedAt { get; set; }

    [JsonPropertyName("submittedIp")]
    public string SubmittedIp { get; set; } = string.Empty;

    [JsonPropertyName("confirmedAt")]
    public DateTimeOffset? ConfirmedAt { get; set; }
}
