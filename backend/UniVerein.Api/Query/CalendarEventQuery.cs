using System;
using System.Collections.Generic;

namespace UniVerein.Api.Query;

public class CalendarEventQuery
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<Guid>? CategoryIds { get; set; }
    public bool MineOnly { get; set; }
}
