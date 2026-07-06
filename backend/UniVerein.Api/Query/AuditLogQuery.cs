using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class AuditLogQuery : QueryBase
{
    public string GetQueryString()
    {
        List<string> queryParams = new()
        {
            $"offset={Offset}",
            $"limit={Limit}",
        };

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}