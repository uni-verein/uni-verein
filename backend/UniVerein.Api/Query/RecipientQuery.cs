using System;
using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class RecipientQuery : QueryBase
{
    public string? Name { get; set; }
    public Guid? CategoryId { get; set; }

    public string GetQueryString()
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(Name))
            queryParams.Add($"name={Uri.EscapeDataString(Name)}");

        if (CategoryId.HasValue)
            queryParams.Add($"categoryId={CategoryId.Value}");

        queryParams.Add($"offset={Offset}");
        queryParams.Add($"limit={Limit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}