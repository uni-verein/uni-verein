using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class ContributionPlanRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [Required]
    [JsonPropertyName("amount")]
    public required decimal Amount { get; set; }

    [Required]
    [JsonPropertyName("interval")]
    public required Interval Interval { get; set; } = Interval.MONTHLY;
}