using System;
using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class ReceiptQuery : QueryBase
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UserId { get; set; }
    public bool? Deleted { get; set; }
    public bool? Paid { get; set; }

    public string GetQueryString()
    {
        List<string> queryParams = new();

        if (DateFrom.HasValue)
            queryParams.Add($"dateFrom={Uri.EscapeDataString(DateFrom.Value.ToString("o"))}");

        if (DateTo.HasValue)
            queryParams.Add($"dateTo={Uri.EscapeDataString(DateTo.Value.ToString("o"))}");

        if (CategoryId.HasValue)
            queryParams.Add($"categoryId={CategoryId.Value}");

        if (UserId.HasValue)
            queryParams.Add($"userId={UserId.Value}");

        if (Deleted.HasValue)
            queryParams.Add($"deleted={Deleted.Value.ToString().ToLower()}");

        if (Paid.HasValue)
            queryParams.Add($"paid={Paid.Value.ToString().ToLower()}");

        queryParams.Add($"offset={Offset}");
        queryParams.Add($"limit={Limit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}
