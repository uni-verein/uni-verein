using System;

namespace UniVerein.Api.Query;

public class IncomeSummaryQuery
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
