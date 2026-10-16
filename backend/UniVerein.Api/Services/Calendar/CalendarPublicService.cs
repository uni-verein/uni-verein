using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public static class CalendarPublicService
{
    public static async Task<CalendarSettingsEntity> GetSettingsAsync(AppDbContext db)
    {
        return await db.CalendarSettings.FirstOrDefaultAsync(s => s.DeletedAt == null)
               ?? new CalendarSettingsEntity();
    }

    public static bool IsAccessible(CalendarSettingsEntity settings, string? key)
    {
        if (!settings.PublicCalendarEnabled)
            return false;

        if (string.IsNullOrEmpty(settings.PublicCalendarKey))
            return true;

        return !string.IsNullOrEmpty(key) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(settings.PublicCalendarKey), Encoding.UTF8.GetBytes(key));
    }

    public static IQueryable<CalendarEventEntity> PubliclyVisible(IQueryable<CalendarEventEntity> events)
    {
        return events.Where(e => e.Visibility == CalendarEventVisibility.CLUB &&
                                 (e.IsPublic || (e.Category != null && e.Category.IsPublic)));
    }

    public static bool IsPublic(CalendarEventEntity calendarEvent)
    {
        return calendarEvent.Visibility == CalendarEventVisibility.CLUB &&
               (calendarEvent.IsPublic || calendarEvent.Category?.IsPublic == true);
    }

    public static string NewKey()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string NewFeedToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
