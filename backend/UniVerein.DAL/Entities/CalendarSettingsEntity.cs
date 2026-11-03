using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("CalendarSettings")]
public class CalendarSettingsEntity : TenantScopedEntity
{
    [Column("public_calendar_enabled")]
    public bool PublicCalendarEnabled { get; set; }

    [Column("public_calendar_title")]
    public string PublicCalendarTitle { get; set; } = string.Empty;

    [Column("public_calendar_key")]
    public string? PublicCalendarKey { get; set; }
}
