using System;
using System.Collections.Generic;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Query;

public class MemberQuery : QueryBase
{
    public string? Name { get; set; }
    public TaskWithinTheClub? TaskWithinTheClub { get; set; }
    public Guid? MemberCategoryId { get; set; }
    public bool? Deleted { get; set; }

    public string GetQueryString()
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(Name))
            queryParams.Add($"name={Uri.EscapeDataString(Name)}");

        if (TaskWithinTheClub.HasValue)
            queryParams.Add($"taskWithinTheClub={TaskWithinTheClub.Value}");

        if (MemberCategoryId.HasValue)
            queryParams.Add($"memberCategoryId={MemberCategoryId.Value}");

        if (Deleted.HasValue)
            queryParams.Add($"deleted={Deleted.Value.ToString().ToLower()}");

        queryParams.Add($"offset={Offset}");
        queryParams.Add($"limit={Limit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}