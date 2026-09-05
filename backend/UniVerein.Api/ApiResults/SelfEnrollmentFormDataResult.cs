using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SelfEnrollmentFormDataResult
{
    [JsonPropertyName("memberCategories")]
    public List<MemberCategoryResult> MemberCategories { get; set; } = new();

    [JsonPropertyName("contributionPlans")]
    public List<ContributionPlanResult> ContributionPlans { get; set; } = new();
}
