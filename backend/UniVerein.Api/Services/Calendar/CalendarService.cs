using System;
using System.Collections.Generic;
using System.Linq;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public static class CalendarService
{
    public static IQueryable<CalendarEventEntity> VisibleTo(IQueryable<CalendarEventEntity> events, Guid userId,
        bool isAdmin)
    {
        return events.Where(e =>
            e.OwnerUserId == userId ||
            e.Visibility == CalendarEventVisibility.CLUB ||
            (e.Visibility == CalendarEventVisibility.PARTICIPANTS &&
             (isAdmin || e.Participants.Any(p => p.UserId == userId))));
    }

    public static bool CanView(CalendarEventEntity calendarEvent, Guid userId, bool isAdmin)
    {
        return calendarEvent.OwnerUserId == userId ||
               calendarEvent.Visibility == CalendarEventVisibility.CLUB ||
               (calendarEvent.Visibility == CalendarEventVisibility.PARTICIPANTS &&
                (isAdmin || calendarEvent.Participants.Any(p => p.UserId == userId)));
    }

    public static bool CanEdit(CalendarEventEntity calendarEvent, Guid userId, bool isAdmin)
    {
        if (calendarEvent.OwnerUserId == userId)
            return true;

        if (calendarEvent.Visibility == CalendarEventVisibility.PRIVATE)
            return false;

        return isAdmin || calendarEvent.Participants.Any(p =>
            p.UserId == userId && p.Role == CalendarParticipantRole.EDITOR);
    }

    public static bool CanDelete(CalendarEventEntity calendarEvent, Guid userId, bool isAdmin)
    {
        if (calendarEvent.OwnerUserId == userId)
            return true;

        return isAdmin && calendarEvent.Visibility != CalendarEventVisibility.PRIVATE;
    }

    public static List<string> ChangedFields(CalendarEventSnapshot before, CalendarEventEntity after)
    {
        List<string> changed = new();
        if (before.Title != after.Title)
            changed.Add("title");
        if (before.Description != after.Description)
            changed.Add("description");
        if (before.StartAt != after.StartAt || before.EndAt != after.EndAt || before.AllDay != after.AllDay)
            changed.Add("time");
        if (before.LocationName != after.LocationName || before.LocationAddress != after.LocationAddress ||
            before.LocationUrl != after.LocationUrl)
            changed.Add("location");
        if (before.Visibility != after.Visibility || before.IsPublic != after.IsPublic)
            changed.Add("visibility");
        if (before.CategoryId != after.CategoryId)
            changed.Add("category");
        if (before.RecurrenceRule != after.RecurrenceRule || before.RecurrenceExceptions != after.RecurrenceExceptions)
            changed.Add("recurrence");
        return changed;
    }

    public static bool IsValidLocationUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static CalendarEventOccurrenceResult ToOccurrence(CalendarEventEntity calendarEvent,
        CalendarOccurrence occurrence, Guid userId, bool isAdmin)
    {
        return new CalendarEventOccurrenceResult
        {
            EventId = calendarEvent.Id,
            Title = calendarEvent.Title,
            Start = occurrence.Start,
            End = occurrence.End,
            IsRecurring = !string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule),
            IsPublic = CalendarPublicService.IsPublic(calendarEvent),
            ReminderMinutesBefore = calendarEvent.ReminderMinutesBefore,
            AllDay = calendarEvent.AllDay,
            LocationName = calendarEvent.LocationName,
            CategoryId = calendarEvent.CategoryId,
            Color = calendarEvent.Category?.Color,
            Visibility = calendarEvent.Visibility,
            CanEdit = CanEdit(calendarEvent, userId, isAdmin),
            MyRsvp = calendarEvent.Participants.FirstOrDefault(p => p.UserId == userId)?.RsvpStatus
        };
    }

    public static CalendarEventResult ToResult(CalendarEventEntity calendarEvent, Guid userId, bool isAdmin,
        IReadOnlyDictionary<Guid, string> usernames, bool isSubscribed)
    {
        return new CalendarEventResult
        {
            Id = calendarEvent.Id,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            Start = calendarEvent.StartAt,
            End = calendarEvent.EndAt,
            AllDay = calendarEvent.AllDay,
            LocationName = calendarEvent.LocationName,
            LocationAddress = calendarEvent.LocationAddress,
            LocationUrl = calendarEvent.LocationUrl,
            Visibility = calendarEvent.Visibility,
            IsPublic = calendarEvent.IsPublic,
            IsPubliclyVisible = CalendarPublicService.IsPublic(calendarEvent),
            RecurrenceRule = calendarEvent.RecurrenceRule,
            RecurrenceExceptions = CalendarRecurrence.ParseExceptions(calendarEvent.RecurrenceExceptions),
            ReminderMinutesBefore = calendarEvent.ReminderMinutesBefore,
            Category = calendarEvent.Category == null
                ? null
                : new CalendarCategoryResult
                {
                    Id = calendarEvent.Category.Id,
                    Name = calendarEvent.Category.Name,
                    Color = calendarEvent.Category.Color,
                    IsPublic = calendarEvent.Category.IsPublic
                },
            Owner = new CalendarUserResult
            {
                Id = calendarEvent.OwnerUserId,
                Username = usernames.GetValueOrDefault(calendarEvent.OwnerUserId, string.Empty)
            },
            Participants = calendarEvent.Participants
                .Select(p => new CalendarEventParticipantResult
                {
                    UserId = p.UserId,
                    Username = usernames.GetValueOrDefault(p.UserId, string.Empty),
                    Role = p.Role,
                    RsvpStatus = p.RsvpStatus
                })
                .OrderBy(p => p.Username)
                .ToList(),
            Version = calendarEvent.Version,
            CreatedAt = calendarEvent.CreatedAt,
            UpdatedAt = calendarEvent.UpdatedAt,
            LastModifiedBy = calendarEvent.LastModifiedByUserId is Guid modifiedBy
                ? new CalendarUserResult
                {
                    Id = modifiedBy,
                    Username = usernames.GetValueOrDefault(modifiedBy, string.Empty)
                }
                : null,
            CanEdit = CanEdit(calendarEvent, userId, isAdmin),
            CanDelete = CanDelete(calendarEvent, userId, isAdmin),
            IsSubscribed = isSubscribed,
            MyRsvp = calendarEvent.Participants.FirstOrDefault(p => p.UserId == userId)?.RsvpStatus
        };
    }
}
