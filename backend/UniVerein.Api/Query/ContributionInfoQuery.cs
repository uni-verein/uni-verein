using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class ContributionInfoQuery : QueryBase
{
    public string GetQueryString()
    {
        List<string> queryParams = new();
        queryParams.Add($"offset={Offset}");
        queryParams.Add($"limit={Limit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}