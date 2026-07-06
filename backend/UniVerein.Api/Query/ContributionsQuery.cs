using System;
using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class ContributionsQuery : QueryBase
{
    public string? Name { get; set; }
    public bool? Unpaid { get; set; }

    public string GetQueryString()
    {
        List<string> queryParams = new();

        if (!string.IsNullOrWhiteSpace(Name))
            queryParams.Add($"name={Uri.EscapeDataString(Name)}");

        if (Unpaid.HasValue)
            queryParams.Add($"unpaid={Unpaid.Value.ToString().ToLower()}");

        queryParams.Add($"offset={Offset}");
        queryParams.Add($"limit={Limit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}