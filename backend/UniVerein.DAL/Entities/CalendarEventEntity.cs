using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("CalendarEvents")]
public class CalendarEventEntity : BaseEntity
{
    [Column("title")]
    public required string Title { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("start_at")]
    public DateTimeOffset StartAt { get; set; }

    [Column("end_at")]
    public DateTimeOffset EndAt { get; set; }

    [Column("all_day")]
    public bool AllDay { get; set; }

    [Column("time_zone_id")]
    public string TimeZoneId { get; set; } = "Europe/Berlin";

    [Column("location_name")]
    public string LocationName { get; set; } = string.Empty;

    [Column("location_address")]
    public string LocationAddress { get; set; } = string.Empty;

    [Column("location_url")]
    public string LocationUrl { get; set; } = string.Empty;

    [Column("visibility")]
    public CalendarEventVisibility Visibility { get; set; } = CalendarEventVisibility.CLUB;

    [Column("owner_user_id")]
    public Guid OwnerUserId { get; set; }

    [ForeignKey("OwnerUserId")]
    public UserEntity? Owner { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public CalendarCategoryEntity? Category { get; set; }

    // iCalendar RRULE without the "RRULE:" prefix, e.g. "FREQ=WEEKLY;BYDAY=WE". Null for single events.
    [Column("recurrence_rule")]
    public string? RecurrenceRule { get; set; }

    // Comma separated UTC start times (ISO 8601) of occurrences that were removed from the series.
    [Column("recurrence_exceptions")]
    public string RecurrenceExceptions { get; set; } = string.Empty;

    // End of the last occurrence, used to find events in a time range without expanding every series.
    // Equals EndAt for single events and a far future date for endless series.
    [Column("series_end_at")]
    public DateTimeOffset SeriesEndAt { get; set; }

    [Column("is_public")]
    public bool IsPublic { get; set; }

    [Column("reminder_minutes_before")]
    public int? ReminderMinutesBefore { get; set; }

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("last_modified_by_user_id")]
    public Guid? LastModifiedByUserId { get; set; }

    public List<CalendarEventParticipantEntity> Participants { get; set; } = new();

    public List<CalendarEventSubscriptionEntity> Subscriptions { get; set; } = new();
}
